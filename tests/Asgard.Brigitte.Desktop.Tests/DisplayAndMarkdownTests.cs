using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Desktop.UI;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>展示真实身份字段，保留 Markdown 换行与结构并禁止执行活动内容。</summary>
public sealed class DisplayAndMarkdownTests
{
    [Fact]
    public void Assignee_and_worker_are_distinct_and_expired_lock_does_not_block()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        var bug = new Bug { AssigneeId = "raw-agent-long-id", LockOwnerId = "worker-7", LockOwnerName = "AI-Worker-7", LockOwnerUserId = "user-a", LockExpireTime = DateTimeOffset.UtcNow.AddMinutes(5) };
        Assert.Equal("用户名称不可用（ID：raw-agent-long-id）", BugDisplay.Assignee(bug.AssigneeId, []));
        Assert.Equal("真实用户", BugDisplay.Claimant(bug, [new IdentityUser { Id = "user-a", DisplayName = "真实用户" }]));
        Assert.Equal("用户名称不可用（ID：user-a）", BugDisplay.Claimant(bug));
        Assert.False(BugDisplay.CanWrite(bug, "other", "user-a"));
        Assert.False(BugDisplay.CanWrite(bug, "worker-7", "user-b"));
        Assert.True(BugDisplay.CanWrite(bug, "worker-7", "user-a"));
        bug.LockOwnerUserId = null;
        Assert.Equal("领取用户未知", BugDisplay.Claimant(bug));
        Assert.False(BugDisplay.CanWrite(bug, "worker-7", "user-a"));
        bug.LockExpireTime = DateTimeOffset.UtcNow.AddSeconds(-1); Assert.True(BugDisplay.CanWrite(bug, "other")); Assert.Equal("未指派", BugDisplay.Assignee(null, []));
    }
    [Fact]
    public void Identity_display_keeps_the_id_and_does_not_guess_unknown_names()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        Assert.Equal("真实用户（登录名）（ID：user-a）", BugDisplay.IdentityWithId("user-a", [new IdentityUser { Id = "user-a", DisplayName = "真实用户", Username = "登录名" }]));
        Assert.Equal("用户名称不可用（ID：missing）", BugDisplay.IdentityWithId("missing", []));
        Assert.Equal("用户名称不可用（ID：empty）", BugDisplay.IdentityWithId("empty", [new IdentityUser { Id = "empty" }]));
        Assert.Equal("未指派", BugDisplay.IdentityWithId(null, []));
    }
    [Fact]
    public void Related_feedback_only_resolves_matching_bugs_and_keeps_their_ids()
    {
        var first = new Bug { Id = "11111111-abcd-4abc-8def-555555555555", Title = "菜单管理" };
        var self = new Bug { Id = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee", Title = "当前反馈" };
        var text = $"提交 1234567890123456789012345678901234567890，关联 {first.Id}、{first.Id.ToUpperInvariant()}，本条 {self.Id}，请求 99999999-8888-4777-8666-555555555555";
        var related = BugReferenceResolver.Resolve(text, [first, self], self.Id);
        Assert.Single(related);
        Assert.Equal("菜单管理", related[0].Title);
        Assert.Equal(first.Id, related[0].Id);
    }
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("[x](javascript:alert%281%29)")]
    [InlineData("![x](data:image/svg+xml;base64,PHN2Zz4=)")]
    [InlineData("[x](https://example.com){onclick=alert(1)}")]
    public void Markdown_has_no_active_html_or_protocol(string markdown)
    {
        var html = SafeMarkdown.Render(markdown); Assert.DoesNotContain("<script", html); Assert.DoesNotContain("href=\"javascript:", html); Assert.DoesNotContain("src=\"data:", html); Assert.DoesNotContain(" onclick=", html);
    }
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Markdown_preserves_single_line_breaks_and_blank_line_paragraphs(string newline)
    {
        var html = SafeMarkdown.Render($"第一行{newline}第二行{newline}{newline}下一段");
        Assert.Contains("第一行<br />\n第二行", html);
        Assert.Contains("<p>下一段</p>", html);
    }
    [Fact]
    public void Markdown_keeps_existing_tables_and_code()
    {
        var html = SafeMarkdown.Render("| A | B |\n|---|---|\n| 1 | 2 |\n\n```cs\nConsole.WriteLine(1);\nConsole.WriteLine(2);\n```");
        Assert.Contains("<table>", html); Assert.Contains("language-cs", html);
        Assert.Contains("Console.WriteLine(1);\nConsole.WriteLine(2);", html);
        Assert.DoesNotContain("<br", html);
    }
}
