using System.Net;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>从真实 Razor 界面检查英文覆盖，用户内容不随界面语言改写。</summary>
public sealed class LanguageRenderTests
{
    [Theory]
    [InlineData(DesktopViewKind.Main, "Bug board")]
    [InlineData(DesktopViewKind.Detail, "Description")]
    [InlineData(DesktopViewKind.Create, "Report bug")]
    [InlineData(DesktopViewKind.Status, "Update status")]
    [InlineData(DesktopViewKind.Comment, "Add reply / work log")]
    [InlineData(DesktopViewKind.Projects, "My projects")]
    [InlineData(DesktopViewKind.Project, "Project details")]
    [InlineData(DesktopViewKind.ProjectCreate, "Create project")]
    [InlineData(DesktopViewKind.Keys, "MCP Key")]
    [InlineData(DesktopViewKind.KeyCreate, "Create MCP key")]
    [InlineData(DesktopViewKind.Users, "User directory")]
    [InlineData(DesktopViewKind.Tray, "Open workspace")]
    [InlineData(DesktopViewKind.Settings, "Follow system")]
    [InlineData(DesktopViewKind.About, "Version")]
    [InlineData(DesktopViewKind.Privacy, "Account and collaboration")]
    [InlineData(DesktopViewKind.JoinProject, "Join a project with an invitation")]
    public async Task Existing_windows_render_in_English(DesktopViewKind kind, string expected)
    {
        using var language = ClientText.UseLanguage("en");
        var raw = await WindowRenderTests.Render("light", kind);
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("lang=\"en\"", html);
        Assert.Contains(expected, html);
        Assert.DoesNotContain("普通设置", html);
        Assert.DoesNotContain("全部优先级", html);
        if (kind is DesktopViewKind.Main or DesktopViewKind.Detail)
            Assert.Contains("刷新列表后筛选条件被重置", html);
        if (kind == DesktopViewKind.Main) Assert.Contains("18 bugs · 18 statuses", html);
        await WindowRenderTests.ExportPreview("english-" + kind.ToString().ToLowerInvariant(), raw);
    }
    [Fact]
    public async Task English_is_available_before_sign_in_and_during_startup_sync()
    {
        using var language = ClientText.UseLanguage("en");
        var welcome = WebUtility.HtmlDecode(await WindowRenderTests.Render("dark", DesktopViewKind.Main, authenticated: false));
        Assert.Contains("Sign in with Heimdall", welcome);
        Assert.DoesNotContain("使用 Heimdall 登录", welcome);
        var startup = WebUtility.HtmlDecode(await WindowRenderTests.Render("light", DesktopViewKind.Main, failProjects: true));
        Assert.Contains("Syncing your workspace", startup);
        Assert.Contains("Received 1,260 / 1,840 changes", startup);
    }
    [Fact]
    public void Callback_uses_the_language_at_its_creation_and_retains_safe_script_encoding()
    {
        LoginCallbackPage page;
        using (ClientText.UseLanguage("en")) page = new LoginCallbackPage();
        using (ClientText.UseLanguage("zh-CN"))
        {
            Assert.Contains("lang=\"en\"", page.Render("<svg></svg>"));
            Assert.Contains("Completing sign-in", page.Render("<svg></svg>"));
            Assert.Contains("Signed in", page.Complete(true));
            Assert.DoesNotContain("登录成功", page.Complete(true));
        }
    }
}
