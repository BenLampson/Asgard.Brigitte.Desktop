using System.Reflection;
using System.Text.Json;
using Asgard.Brigitte.Sync;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Desktop.UI;
using Asgard.Heimdall.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>用实际 Razor 组件检查日夜模式和全部独立业务窗口。</summary>
public sealed class WindowRenderTests
{
    /// <summary>通过实际父页面验证字符串参数绑定，避免组件单测掩盖字面量传参错误。</summary>
    [Fact]
    public async Task Workspace_passes_current_status_to_collapsed_filters()
    {
        var html = System.Net.WebUtility.HtmlDecode(await Render("dark", DesktopViewKind.Main));
        Assert.Contains("status-summary-value\">全部状态</span>", html);
        Assert.DoesNotContain(">_status<", html);
        Assert.Contains("advanced-disclosure", html);
        Assert.DoesNotContain("class=\"advanced-disclosure\" open", html);
    }
    /// <summary>更换欢迎页后，未登录用户仍不能进入已缓存的业务工作区。</summary>
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Signed_out_window_offers_login_without_rendering_cached_workspace(string theme)
    {
        var html = await Render(theme, DesktopViewKind.Main, authenticated: false);
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("使用 Heimdall 登录", decoded);
        Assert.DoesNotContain("刷新列表后筛选条件被重置", decoded);
        Assert.DoesNotContain("正在同步你的工作区", decoded);
        await ExportPreview("login-" + theme, html);
    }
    /// <summary>无参数的同步页必须主动响应状态变化，不能一直停留在第一次渲染的“未登录”。</summary>
    [Fact]
    public async Task Startup_updates_status_and_progress_without_parent_parameters_changing()
    {
        var host = new RenderHost("light") { RealtimeStatus = "未登录", SyncHasManifest = false };
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IClientHost>(host);
        using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var view = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<SyncStartupView>());
        await renderer.Dispatcher.InvokeAsync(() => { host.RealtimeStatus = "服务端同步失败，正在自动重试"; host.PublishStatus(); });
        var errorHtml = await renderer.Dispatcher.InvokeAsync(() => System.Net.WebUtility.HtmlDecode(view.ToHtmlString()));
        Assert.Contains("服务端同步失败", errorHtml); Assert.DoesNotContain("未登录", errorHtml);
        await renderer.Dispatcher.InvokeAsync(() => { host.RealtimeStatus = "正在同步数据"; host.SyncHasManifest = true; host.PublishStatus(); });
        var progressHtml = await renderer.Dispatcher.InvokeAsync(() => view.ToHtmlString());
        Assert.Contains("value=\"68\"", progressHtml);
    }
    /// <summary>详情正文必须渲染真实字段，不能把 Razor 表达式当成字符串。</summary>
    [Fact]
    public async Task Detail_renders_markdown_from_bug_fields()
    {
        var html = System.Net.WebUtility.HtmlDecode(await Render("dark", DesktopViewKind.Detail));
        Assert.Contains("<p>问题描述</p>", html);
        Assert.Contains("<h2>复现步骤</h2>", html);
        Assert.Contains("<li>打开看板</li>", html);
        Assert.DoesNotContain("<p>bug.", html);
    }

    /// <summary>失败不能误导为没有项目权限。</summary>
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Startup_waits_for_sync_instead_of_rendering_stale_or_empty_projects(string theme)
    {
        var original = await Render(theme, DesktopViewKind.Main, true);
        var html = System.Net.WebUtility.HtmlDecode(original);
        Assert.Contains("正在同步你的工作区", html); Assert.DoesNotContain("暂无可访问项目", html);
        Assert.Contains("value=\"68\"", html);
        await ExportPreview("sync-" + theme, original);
    }
    [Theory]
    [InlineData("light", false)]
    [InlineData("dark", false)]
    [InlineData("light", true)]
    [InlineData("dark", true)]
    public async Task Workspace_renders_all_bugs_and_real_identity_fields(string theme, bool list)
    {
        var html = await Render(theme, DesktopViewKind.Main, list: list);
        Assert.Contains("client " + theme, html);
        if (list) Assert.Equal(18, html.Split("class=\"bug-cell\"").Length - 1);
        else Assert.Equal(18, html.Split("class=\"lane\"").Length - 1);
        // 已处理、已回归和最终结束状态的卡片在默认看板与列表中都不能丢失。
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        for (var i = 1; i < 18; i++) Assert.Contains("状态更新后卡片及时显示 · " + i + "\"", decoded);
        Assert.Contains(ClientText.T("{0} 条 BUG · {1} 个状态", 18, 18), decoded);
        Assert.Contains("raw-owner-id-very-long", html); Assert.Contains("测试用户", decoded); Assert.DoesNotContain("AI-Worker-7", html); Assert.DoesNotContain("还有 3 条", html);
        await ExportPreview(list ? "list-" + theme : theme, html);
    }
    [Theory]
    [InlineData(DesktopViewKind.Detail)]
    [InlineData(DesktopViewKind.Create)]
    [InlineData(DesktopViewKind.Edit)]
    [InlineData(DesktopViewKind.Status)]
    [InlineData(DesktopViewKind.Comment)]
    [InlineData(DesktopViewKind.Projects)]
    [InlineData(DesktopViewKind.Project)]
    [InlineData(DesktopViewKind.ProjectEdit)]
    [InlineData(DesktopViewKind.ProjectCreate)]
    [InlineData(DesktopViewKind.Keys)]
    [InlineData(DesktopViewKind.KeyCreate)]
    [InlineData(DesktopViewKind.Users)]
    [InlineData(DesktopViewKind.Tray)]
    public async Task Independent_existing_windows_render_without_component_failures(DesktopViewKind kind)
    {
        var html = await Render("light", kind); Assert.NotEmpty(html); Assert.DoesNotContain("API 返回空响应", System.Net.WebUtility.HtmlDecode(html));
        if (kind == DesktopViewKind.Tray)
        {
            await ExportPreview("tray-light", html);
            await ExportPreview("tray-dark", await Render("dark", kind));
        }
    }
    /// <summary>可选导出实际组件与正式样式，供浏览器检查布局；不会进入产品构建。</summary>
    internal static async Task ExportPreview(string name, string html)
    {
        if (Environment.GetEnvironmentVariable("BRIGITTE_VALIDATE_LAYOUT") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var assets = Path.Combine(root, "src/Asgard.Brigitte.Desktop.UI/wwwroot/");
        var css = await File.ReadAllTextAsync(Path.Combine(assets, "styles.css"), TestContext.Current.CancellationToken)
            + await File.ReadAllTextAsync(Path.Combine(assets, "branding.css"), TestContext.Current.CancellationToken)
            + await File.ReadAllTextAsync(Path.Combine(assets, "brigid-motion.css"), TestContext.Current.CancellationToken)
            + await File.ReadAllTextAsync(Path.Combine(assets, "preferences.css"), TestContext.Current.CancellationToken);
        html = html.Replace("_content/Asgard.Brigitte.Desktop.UI/", new Uri(assets).AbsoluteUri);
        var output = Path.Combine(root, "tests/Asgard.Brigitte.Desktop.Tests/obj/ui-validation"); Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, name + ".html"), "<!doctype html><html lang='" + ClientText.Language + "'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><style>" + css + "</style><body><div id='app'>" + html + "</div></body></html>", TestContext.Current.CancellationToken);
    }
    internal static async Task<string> Render(string theme, DesktopViewKind kind, bool failProjects = false, bool authenticated = true, bool list = false)
    {
        using var transport = new TestTransport(_ => throw new InvalidOperationException("渲染业务界面不得读取 REST"));
        using var local = new SyncTestWorkspace();
        var rows = new List<SyncRow>();
        foreach (var (path, kindName) in new[] { ("projects", "project"), ("bugs", "bug"), ("identity/users", "user") })
        {
            using var response = Response(new Uri("https://local.test/api/brigitte/" + path));
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var item in payload.RootElement.GetProperty("data").EnumerateArray()) rows.Add(SyncProtocol.Row(kindName, item.GetProperty("id").GetString()!, item));
        }
        rows.Add(SyncProtocol.Row("access", "current", new CurrentAccess { IsTenantAdmin = true }));
        rows.Add(SyncProtocol.Row("prompt", "fix", new LocalPromptTemplate { Key = "fix", ActionLabel = "复制修复", Category = "bug", Enabled = true, ApplicableStatuses = ["processing"], PrimaryStatuses = ["processing"] }));
        if (kind == DesktopViewKind.Tray)
            foreach (var name in new[] { "客服系统", "我的项目", "CAD", "SCM 供应链" })
                rows.Add(SyncProtocol.Row("project", name, new Project { Id = name, Name = name, Code = name, Status = "active" }));
        local.Replace(rows.ToArray());
        var store = new RenderCredentialStore();
        using var session = new SessionService(Options.Create(new ClientOptions()), transport, store, store);
        // 只在测试程序集注入内存身份；产品没有跳过登录的参数或入口。
        if (authenticated) typeof(SessionService).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, new StoredSession { Tokens = new HeimdallTokenSet { AccessToken = "render-test-only", ExpiresIn = 3600 }, SubjectId = "user", SessionId = "session", DisplayName = "测试账号", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>(); services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions())); services.AddSingleton(session); services.AddSingleton<IClientSession>(session); services.AddSingleton<IHttpClientFactory>(transport); services.AddSingleton(local.Database); services.AddSingleton<LocalWorkspaceReader>(); services.AddSingleton<IWorkspaceSync>(new TestWorkspaceSync()); services.AddSingleton<BrigitteApi>(); services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json"))); services.AddSingleton<IClientHost>(new RenderHost(theme) { SyncReady = !failProjects });
        if (list) services.AddSingleton<IComponentActivator>(new ListRenderActivator());
        using var provider = services.BuildServiceProvider(); await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        // 原生宿主显示托盘前会先加载项目缓存，测试使用相同的准备顺序。
        if (kind == DesktopViewKind.Tray) await provider.GetRequiredService<WorkspaceStore>().LoadAsync();
        return await renderer.Dispatcher.InvokeAsync(async () => { var view = await renderer.RenderComponentAsync<AppView>(ParameterView.FromDictionary(new Dictionary<string, object?> { { nameof(AppView.View), new DesktopView(kind, "bug", "project") } })); return view.ToHtmlString(); });
    }
    private static HttpResponseMessage Response(Uri uri)
    {
        object data; var paged = false; var total = 1; var path = uri.AbsolutePath;
        var project = new Project { Id = "project", Name = "Brigitte", Code = "brigitte", Status = "active", OwnerId = "owner", MemberUserIds = ["owner"] };
        var bugs = Enumerable.Range(0, 18).Select(i => new Bug { Id = i == 0 ? "bug" : "bug-" + i, ProjectId = "project", Title = i == 0 ? "刷新列表后筛选条件被重置" : "状态更新后卡片及时显示 · " + i, Description = "问题描述", Content = "## 复现步骤\n1. 打开看板\n2. 点击刷新", Status = "processing", Priority = "high", AssigneeId = "raw-owner-id-very-long", LockOwnerId = i == 0 ? "worker-7" : null, LockOwnerName = i == 0 ? "AI-Worker-7" : null, LockOwnerUserId = i == 0 ? "owner" : null, LockExpireTime = DateTimeOffset.UtcNow.AddHours(1), CreateTime = DateTimeOffset.UtcNow }).ToList();
        // 六条完成记录必须与处理中记录一起显示，覆盖之前缺失的四个最终状态。
        var completed = new[] { "fixed", "regressed", "closed", "wont_fix", "duplicate", "cannot_reproduce" };
        for (var i = 0; i < completed.Length; i++) bugs[12 + i].Status = completed[i];
        if (path.EndsWith("/projects")) { data = new[] { project }; paged = true; }
        else if (path.EndsWith("/projects/project")) data = project;
        else if (path.EndsWith("/bugs/statistics")) data = new[] { new ProjectStatistics { ProjectId = "project", Total = 18, HighPriority = 18 } };
        else if (path.EndsWith("/identity/access")) data = new CurrentAccess { IsTenantAdmin = true };
        else if (path.EndsWith("/identity/users")) { data = new[] { new IdentityUser { Id = "owner", DisplayName = "测试用户", Username = "owner", Status = 1 } }; paged = true; }
        else if (path.EndsWith("/bugs")) { data = bugs; total = bugs.Count; paged = true; }
        else if (path.EndsWith("/bugs/bug")) data = new BugDetail { Bug = bugs[0] };
        else if (path.EndsWith("/bug-actions")) data = new[] { new PromptAction { Key = "fix", ActionLabel = "复制修复", IsPrimary = true } };
        else if (path.EndsWith("/mcp-api-keys")) data = Array.Empty<McpKey>();
        else throw new InvalidOperationException("未定义的测试端点：" + path);
        return TestTransport.Json(JsonSerializer.Serialize(paged ? new { code = 200, data, totalCount = total } : (object)new { code = 200, data }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
