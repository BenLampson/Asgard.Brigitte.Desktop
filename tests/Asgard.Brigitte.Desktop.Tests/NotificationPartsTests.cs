using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Sync;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>长通知分条保留全文，项目和 BUG 名称不再静默截短。</summary>
public sealed class NotificationPartsTests
{
    [Fact]
    public void Long_unicode_notification_roundtrips_without_omission()
    {
        var text = string.Concat(Enumerable.Repeat("项目😀<&>\n", 400));
        var parts = NotificationParts.Split(text);
        Assert.Equal(text, string.Concat(parts));
        Assert.All(parts, part => { Assert.True(part.Length <= 500); Assert.False(char.IsHighSurrogate(part[^1])); });
    }
    [Fact]
    public void Stored_notice_keeps_full_project_and_bug_names()
    {
        using var local = new SyncTestWorkspace();
        var projectName = new string('项', 180);
        var title = new string('题', 240);
        var project = SyncProtocol.Row("project", "p", new { id = "p", name = projectName });
        local.Replace(project);
        local.Replace(project, SyncProtocol.Row("bug", "b", new { id = "b", projectId = "p", title, status = "waiting" }));
        var notice = Assert.Single(local.Database.Notices());
        Assert.Contains(projectName, notice.Message);
        Assert.Contains(title, notice.Message);
        Assert.DoesNotContain("…", notice.Message);
    }
}
