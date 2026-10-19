using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Desktop.UI;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>展示真实身份字段并禁止 Markdown 执行活动内容。</summary>
public sealed class DisplayAndMarkdownTests
{
    [Fact]
    public void Assignee_and_worker_are_distinct_and_expired_lock_does_not_block()
    {
        var bug = new Bug { AssigneeId = "raw-agent-long-id", LockOwnerId = "worker-7", LockOwnerName = "AI-Worker-7", LockOwnerUserId = "user-a", LockExpireTime = DateTimeOffset.UtcNow.AddMinutes(5) };
        Assert.Equal("raw-agent-long-id", BugDisplay.Assignee(bug.AssigneeId, []));
        Assert.Equal("真实用户", BugDisplay.Claimant(bug, [new IdentityUser { Id = "user-a", DisplayName = "真实用户" }]));
        Assert.Equal("user-a", BugDisplay.Claimant(bug));
        Assert.False(BugDisplay.CanWrite(bug, "other", "user-a"));
        Assert.False(BugDisplay.CanWrite(bug, "worker-7", "user-b"));
        Assert.True(BugDisplay.CanWrite(bug, "worker-7", "user-a"));
        bug.LockOwnerUserId = null;
        Assert.Equal("领取用户未知", BugDisplay.Claimant(bug));
        Assert.False(BugDisplay.CanWrite(bug, "worker-7", "user-a"));
        bug.LockExpireTime = DateTimeOffset.UtcNow.AddSeconds(-1); Assert.True(BugDisplay.CanWrite(bug, "other")); Assert.Equal("未指派", BugDisplay.Assignee(null, []));
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
    [Fact]
    public void Markdown_keeps_existing_tables_and_code()
    {
        var html = SafeMarkdown.Render("| A | B |\n|---|---|\n| 1 | 2 |\n\n```cs\nConsole.WriteLine(1);\n```"); Assert.Contains("<table>", html); Assert.Contains("language-cs", html);
    }
}
