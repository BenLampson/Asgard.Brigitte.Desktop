using System.Net;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>通过完整设置页面检查实际项目目录、全部状态和中英文渲染。</summary>
public sealed class NotificationSettingsRenderTests
{
    /// <summary>桌面两种主题和两种语言均能访问默认规则及项目配置。</summary>
    [Theory]
    [InlineData("light", "zh-CN")]
    [InlineData("dark", "zh-CN")]
    [InlineData("light", "en")]
    [InlineData("dark", "en")]
    public async Task Settings_render_project_rules_and_all_destination_statuses(string theme, string language)
    {
        using var scope = ClientText.UseLanguage(language);
        var html = await WindowRenderTests.Render(theme, DesktopViewKind.Settings);
        var decoded = WebUtility.HtmlDecode(html);
        Assert.Contains(ClientText.T("提醒设置"), decoded);
        Assert.Contains(ClientText.T("默认规则（未单独配置的项目）"), decoded);
        Assert.Contains("value=\"project\"", html);
        Assert.Contains("notification-search", html);
        foreach (var label in BugDisplay.Statuses.Values) Assert.Contains(label, decoded);
        Assert.Contains(ClientText.T("领取、释放与负责人变化"), decoded);
        if (language == "en") Assert.DoesNotContain("提醒设置", decoded);
        await WindowRenderTests.ExportPreview("notifications-" + theme + "-" + language, html);
    }

    /// <summary>未登录时不展示缓存项目或允许编辑别人的规则。</summary>
    [Fact]
    public async Task Signed_out_settings_do_not_expose_project_controls()
    {
        var html = await WindowRenderTests.Render("light", DesktopViewKind.Settings, authenticated: false);
        Assert.DoesNotContain("id=\"notification-project\"", html);
        Assert.Contains(ClientText.T("登录在线工作区后，可以按项目配置提醒。本机工作区不会产生在线提醒。"), WebUtility.HtmlDecode(html));
    }
}
