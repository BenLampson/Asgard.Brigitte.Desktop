using System.Net;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证主窗口启动时的会话恢复与本机入口。</summary>
public sealed class StartupSessionRenderTests
{
    /// <summary>有效的已保存凭据在启动时恢复，不打开浏览器或显示入口选择。</summary>
    [Fact]
    public async Task Main_window_restores_saved_session_and_starts_workspace_sync()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        using var local = new StandaloneTestWorkspace();
        using var server = new SignedOidcTransport(new SessionTestClock());
        var credentials = new MemoryCredentialStore { Value = server.Saved() };
        using var session = server.Session(credentials);

        var html = await RenderAsync(local, session, server, DesktopViewKind.Main);

        Assert.True(session.IsAuthenticated);
        Assert.Equal("保存的账号", session.DisplayName);
        Assert.Contains("正在同步你的工作区", html);
        Assert.DoesNotContain("使用 Heimdall 登录", html);
        Assert.Equal(1, credentials.Reads);
        Assert.Equal(1, server.Refreshes);
        Assert.Equal(0, credentials.BrowserOpens);
        Assert.False(local.Database.Ready);
    }

    /// <summary>在线服务暂不可用时保留凭据与本机入口，可在本机继续工作。</summary>
    [Fact]
    public async Task Failed_startup_restore_keeps_credentials_and_allows_local_workspace()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        using var local = new StandaloneTestWorkspace();
        using var server = new SignedOidcTransport(new SessionTestClock()) { Offline = true };
        var saved = server.Saved();
        var credentials = new MemoryCredentialStore { Value = saved };
        using var session = server.Session(credentials);

        var welcome = await RenderAsync(local, session, server, DesktopViewKind.Main);

        Assert.False(session.IsAuthenticated);
        Assert.True(session.RestorePending);
        Assert.Contains("重试恢复登录", welcome);
        Assert.Contains("本机使用", welcome);
        Assert.DoesNotContain(saved, welcome);
        Assert.Equal(saved, credentials.Value);
        Assert.Equal(0, credentials.Clears);
        Assert.Equal(0, credentials.BrowserOpens);

        local.Database.Open();
        await session.UseLocalAsync(TestContext.Current.CancellationToken);
        var requests = server.Requests;
        var workspace = await RenderAsync(local, session, server, DesktopViewKind.Main);
        Assert.Contains("本机工作区", workspace);
        Assert.True(session.IsLocal);
        Assert.Equal(requests, server.Requests);
        Assert.Equal(saved, credentials.Value);
    }

    /// <summary>刷新凭据明确失效时返回欢迎页，不自动打开新的登录流程。</summary>
    [Fact]
    public async Task Invalid_saved_session_returns_to_welcome_without_opening_browser()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        using var local = new StandaloneTestWorkspace();
        using var server = new SignedOidcTransport(new SessionTestClock()) { InvalidGrant = true };
        var credentials = new MemoryCredentialStore { Value = server.Saved() };
        using var session = server.Session(credentials);

        var html = await RenderAsync(local, session, server, DesktopViewKind.Main);

        Assert.Contains("本机使用", html);
        Assert.Contains("使用 Heimdall 登录", html);
        Assert.False(session.HasWorkspace);
        Assert.False(session.RestorePending);
        Assert.Null(credentials.Value);
        Assert.Equal(0, credentials.BrowserOpens);
    }

    /// <summary>辅助窗口不独立读取凭据或发起刷新。</summary>
    [Fact]
    public async Task Secondary_window_does_not_restore_saved_session()
    {
        using var local = new StandaloneTestWorkspace();
        using var server = new SignedOidcTransport(new SessionTestClock());
        var credentials = new MemoryCredentialStore { Value = server.Saved() };
        using var session = server.Session(credentials);

        await RenderAsync(local, session, server, DesktopViewKind.Tray);

        Assert.False(session.HasWorkspace);
        Assert.Equal(0, credentials.Reads);
        Assert.Equal(0, server.Requests);
    }

    /// <summary>显式退出后，同一个主窗口停留在欢迎页，不重新恢复已撤销的会话。</summary>
    [Fact]
    public async Task Explicit_logout_stays_on_welcome_in_the_existing_main_window()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        using var local = new StandaloneTestWorkspace();
        using var server = new SignedOidcTransport(new SessionTestClock());
        var credentials = new MemoryCredentialStore { Value = server.Saved() };
        using var session = server.Session(credentials);
        await using var provider = CreateProvider(local, session, server);
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<AppView>());

        Assert.True(session.IsAuthenticated);
        await renderer.Dispatcher.InvokeAsync(() => session.LogoutAsync());
        var html = await renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(root.ToHtmlString()));

        Assert.Contains("使用 Heimdall 登录", html);
        Assert.False(session.HasWorkspace);
        Assert.Null(credentials.Value);
        Assert.Equal(1, server.Refreshes);
        Assert.Equal(0, credentials.BrowserOpens);
    }

    /// <summary>使用真实根组件及专用测试服务呈现启动结果。</summary>
    private static async Task<string> RenderAsync(StandaloneTestWorkspace local, SessionService session, SignedOidcTransport server, DesktopViewKind kind)
    {
        await using var provider = CreateProvider(local, session, server);
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(AppView.View)] = new DesktopView(kind) });
            var root = await renderer.RenderComponentAsync<AppView>(parameters);
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
    }

    /// <summary>在线请求仅使用本地签名的协议替身，业务数据库使用专用临时目录。</summary>
    private static ServiceProvider CreateProvider(StandaloneTestWorkspace local, SessionService session, SignedOidcTransport server)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<ClientOptions>>(Options.Create(server.Options));
        services.AddSingleton(session); services.AddSingleton<IClientSession>(session);
        services.AddSingleton(local.Database); services.AddSingleton(local.OnlineDatabase);
        services.AddSingleton<IHttpClientFactory>(server); services.AddSingleton<IWorkspaceSync>(local.Sync);
        services.AddSingleton<LocalWorkspaceReader>(); services.AddSingleton<BrigitteApi>(); services.AddSingleton<WorkspaceStore>();
        services.AddSingleton(new BoardPreferenceStore(Path.Combine(local.DirectoryPath, "board.json")));
        services.AddSingleton<IClientHost>(new RenderHost("light") { SyncReady = false });
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        return services.BuildServiceProvider();
    }
}
