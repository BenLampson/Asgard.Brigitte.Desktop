using System.Net;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Xunit;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>使用实际窗口验证在线项目权限入口。</summary>
public sealed class ProjectPermissionRenderTests
{
    [Theory]
    [InlineData(true, "admin", true)]
    [InlineData(false, "owner", true)]
    [InlineData(false, "member", false)]
    [InlineData(false, "other-owner", false)]
    [InlineData(false, "", false)]
    public async Task Invite_entry_requires_admin_or_this_project_owner(bool admin, string user, bool expected)
    {
        foreach (var view in new[] { DesktopViewKind.Projects, DesktopViewKind.Main })
        {
            var html = WebUtility.HtmlDecode(await WindowRenderTests.Render("light", view, isTenantAdmin: admin, userId: user));
            if (expected) Assert.Contains("邀请成员", html);
            else Assert.DoesNotContain("邀请成员", html);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Empty_online_workspace_only_offers_creation_to_admin(bool admin)
    {
        var html = WebUtility.HtmlDecode(await WindowRenderTests.Render("light", DesktopViewKind.Main, isTenantAdmin: admin, emptyProjects: true));
        Assert.Contains("通过邀请加入", html);
        if (admin) Assert.Contains("创建项目", html);
        else Assert.DoesNotContain("创建项目", html);
    }

    [Fact]
    public async Task Non_admin_empty_workspace_uses_english_guidance()
    {
        using var language = ClientText.UseLanguage("en");
        var html = WebUtility.HtmlDecode(await WindowRenderTests.Render("light", DesktopViewKind.Main, isTenantAdmin: false, emptyProjects: true));
        Assert.Contains("No projects yet", html);
        Assert.Contains("Join a project with an invitation link, or contact a tenant administrator.", html);
    }

    [Fact]
    public async Task Non_admin_cannot_open_online_project_creation_form()
    {
        var html = WebUtility.HtmlDecode(await WindowRenderTests.Render("light", DesktopViewKind.ProjectCreate, isTenantAdmin: false));
        Assert.Contains("仅租户管理员可以创建在线项目。", html);
        Assert.DoesNotContain("保存项目", html);
    }
}
