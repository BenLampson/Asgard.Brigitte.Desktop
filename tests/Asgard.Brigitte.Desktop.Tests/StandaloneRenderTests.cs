using System.Net;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>真实 Razor 组件验证默认入口、本机各业务窗口和在线能力的可见边界。</summary>
public sealed class StandaloneRenderTests
{
    /// <summary>即使保存了在线凭据，每次启动先显示入口且不读取凭据或等待网络。</summary>
    [Theory]
    [InlineData("zh-CN", "本机使用", "使用 Heimdall 登录")]
    [InlineData("en", "Use on this computer", "Sign in with Heimdall")]
    public async Task Startup_always_offers_local_first_without_restoring_online_credentials(string language, string localLabel, string onlineLabel)
    {
        using var scope = ClientText.UseLanguage(language);
        using var local = new StandaloneTestWorkspace();
        var html = await Render(local, DesktopViewKind.Main);
        Assert.True(html.IndexOf(localLabel, StringComparison.Ordinal) < html.IndexOf(onlineLabel, StringComparison.Ordinal));
        Assert.Contains("brand-login brand-login-secondary", html);
        Assert.False(local.Session.HasWorkspace); Assert.False(local.Database.Ready);
        Assert.Equal(0, local.Credentials.Reads); Assert.Equal(0, local.Transport.Count);
        await WindowRenderTests.ExportPreview("local-entry-" + language, "<div class='client light'>" + html + "</div>");
    }

    /// <summary>进入本机后展示真实记录、完整业务窗口，不展示账号、邀请与远程 Key。</summary>
    [Theory]
    [InlineData("light", "zh-CN")]
    [InlineData("dark", "zh-CN")]
    [InlineData("light", "en")]
    [InlineData("dark", "en")]
    public async Task Local_workspace_and_windows_are_usable_without_online_identity(string theme, string languageCode)
    {
        using var language = ClientText.UseLanguage(languageCode);
        using var local = new StandaloneTestWorkspace(); await local.EnterAsync();
        var empty = await Render(local, DesktopViewKind.Main, theme: theme);
        Assert.Contains(ClientText.T("创建项目"), empty); Assert.DoesNotContain(ClientText.T("通过邀请加入"), empty);
        var project = await local.ProjectAsync();
        var bug = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "本机问题", Content = "## 断网操作步骤", Priority = "high" });
        var main = await Render(local, DesktopViewKind.Main, bug.Id, project.Id, theme);
        Assert.Contains("本机问题", main); Assert.Contains(ClientText.T("本机工作区 · 数据仅保存在这台电脑"), main); Assert.Contains(ClientText.T("返回启动页"), main);
        Assert.Contains(languageCode == "en" ? "Copy repair prompt" : "复制修复提示词", main);
        Assert.DoesNotContain("MCP Key", main); Assert.DoesNotContain("账号安全", main); Assert.DoesNotContain("连接已中断", main);
        await WindowRenderTests.ExportPreview("local-workspace-" + theme + "-" + languageCode, main);
        foreach (var kind in new[] { DesktopViewKind.Detail, DesktopViewKind.Create, DesktopViewKind.Edit, DesktopViewKind.Status, DesktopViewKind.Comment, DesktopViewKind.Projects, DesktopViewKind.Project, DesktopViewKind.ProjectCreate, DesktopViewKind.ProjectEdit, DesktopViewKind.Tray })
        {
            var html = await Render(local, kind, kind is DesktopViewKind.Project or DesktopViewKind.ProjectEdit ? project.Id : bug.Id, project.Id, theme);
            Assert.DoesNotContain("尚未打开", html); Assert.DoesNotContain("正在同步你的工作区", html);
            Assert.DoesNotContain(ClientText.T("邀请成员"), html); Assert.DoesNotContain(ClientText.T("通过邀请加入"), html);
            if (kind == DesktopViewKind.Detail) Assert.Contains("断网操作步骤", html);
        }
        await local.Session.LogoutAsync();
        var welcome = await Render(local, DesktopViewKind.Main);
        Assert.Contains(ClientText.T("使用 Heimdall 登录"), welcome); Assert.DoesNotContain("本机问题", welcome);
        await local.EnterAsync();
        Assert.Contains("本机问题", await Render(local, DesktopViewKind.Main));
        Assert.Equal(0, local.Transport.Count); Assert.Equal(0, local.Credentials.Reads);
    }

    /// <summary>使用正式 DI 关系，确认本机与在线读取器按会话选择而不是依赖测试替身。</summary>
    private static async Task<string> Render(StandaloneTestWorkspace local, DesktopViewKind kind, string? id = null, string? project = null, string theme = "light")
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton(local.Session); services.AddSingleton<IClientSession>(local.Session);
        services.AddSingleton(local.Database); services.AddSingleton(local.OnlineDatabase); services.AddSingleton<IHttpClientFactory>(local.Transport);
        services.AddSingleton<IWorkspaceSync>(local.Sync); services.AddSingleton<LocalWorkspaceReader>(); services.AddSingleton<BrigitteApi>();
        services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json"))); services.AddSingleton<IClientHost>(new RenderHost(theme) { SyncReady = false });
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        await using var provider = services.BuildServiceProvider();
        // 容器只负责渲染器，夹具负责会话与数据库寿命，避免跨窗口提前释放同一实例。
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        if (kind == DesktopViewKind.Tray) await provider.GetRequiredService<WorkspaceStore>().LoadAsync();
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AppView>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(AppView.View)] = new DesktopView(kind, id, project) }));
            return WebUtility.HtmlDecode(component.ToHtmlString());
        });
    }
}
