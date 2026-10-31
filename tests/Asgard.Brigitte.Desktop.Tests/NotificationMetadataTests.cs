using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>从真实本地同步提交验证提醒元数据，静默不会漏掉业务投影或未读动态。</summary>
public sealed class NotificationMetadataTests
{
    /// <summary>状态历史与同批次回复合并后仍保留目标状态和独立类型，且不依赖界面语言。</summary>
    [Fact]
    public void Committed_notices_include_project_types_and_all_status_destinations()
    {
        using var local = new SyncTestWorkspace();
        var project = SyncProtocol.Row("project", "p", new { id = "p", name = "项目名称" });
        var bug = SyncProtocol.Row("bug", "b", new { id = "b", projectId = "p", title = "标题", status = "waiting" });
        local.Replace(project, bug);
        IReadOnlyList<SyncNotice> notices = [];
        local.Database.Committed += commit => notices = commit.Notices;
        var changed = SyncProtocol.Row("bug", "b", new { id = "b", projectId = "p", title = "新标题", status = "pending_regression", assigneeId = "u" });
        var history1 = SyncProtocol.Row("history", "h1", new { id = "h1", bugId = "b", fromStatus = "waiting", toStatus = "processing", createTime = "2026-10-01T00:00:00Z" });
        var history2 = SyncProtocol.Row("history", "h2", new { id = "h2", bugId = "b", fromStatus = "processing", toStatus = "pending_regression", createTime = "2026-10-01T00:01:00Z" });
        var comment = SyncProtocol.Row("comment", "c", new { id = "c", bugId = "b", content = "不在通知中展示", commentType = "fix" });
        var attachment = SyncProtocol.Row("attachment", "a", new { id = "a", bugId = "b", fileName = "test.txt" });
        local.Replace(project, changed, history1, history2, comment, attachment);
        var notice = Assert.Single(notices);
        Assert.Equal("p", notice.ProjectId);
        Assert.Equal(NotificationChange.Status | NotificationChange.Details | NotificationChange.Assignment |
            NotificationChange.Comments | NotificationChange.Attachments, notice.Changes);
        Assert.Equal(new[] { "pending_regression", "processing" }, notice.TargetStatuses.Order().ToArray());
        Assert.DoesNotContain("不在通知中展示", notice.Message);
        using (ClientText.UseLanguage("en")) Assert.True(new NotificationRule { Changes = NotificationChange.Status, Statuses = ["pending_regression"] }.Allows(notice));
        Assert.False(new NotificationRule { Enabled = false }.Allows(notice));
        Assert.Single(local.Database.Read<Core.Models.Bug>("bug"));
        Assert.Single(local.Database.Notices());
    }

    /// <summary>没有状态历史的投影变化也能提醒；新建 BUG 的初始状态不冒充状态变更。</summary>
    [Fact]
    public void New_bug_and_status_without_history_are_classified_separately()
    {
        using var local = new SyncTestWorkspace();
        var project = SyncProtocol.Row("project", "p", new { id = "p", name = "项目" });
        local.Replace(project);
        IReadOnlyList<SyncNotice> notices = [];
        local.Database.Committed += commit => notices = commit.Notices;
        SyncRow Bug(string status) => SyncProtocol.Row("bug", "b", new { id = "b", projectId = "p", title = "标题", status });
        local.Replace(project, Bug("processing"));
        var created = Assert.Single(notices);
        Assert.Equal(NotificationChange.Created, created.Changes);
        Assert.Empty(created.TargetStatuses);
        Assert.False(new NotificationRule { Changes = NotificationChange.Status }.Allows(created));
        local.Replace(project, Bug("closed"));
        var updated = Assert.Single(notices);
        Assert.Equal(NotificationChange.Status, updated.Changes);
        Assert.Equal(["closed"], updated.TargetStatuses);
    }

    /// <summary>删除时仍使用删除前的归属，不能让已静默项目的移除提示漏过筛选。</summary>
    [Fact]
    public void Removal_keeps_project_identity_and_does_not_repeat_child_changes()
    {
        using var local = new SyncTestWorkspace();
        local.Replace(SyncProtocol.Row("project", "p", new { id = "p", name = "项目" }),
            SyncProtocol.Row("bug", "b", new { id = "b", projectId = "p", title = "标题" }),
            SyncProtocol.Row("comment", "c", new { id = "c", bugId = "b" }));
        IReadOnlyList<SyncNotice> notices = [];
        local.Database.Committed += commit => notices = commit.Notices;
        local.Replace();
        Assert.Equal(2, notices.Count);
        Assert.All(notices, notice => Assert.Equal("p", notice.ProjectId));
        Assert.Contains(notices, notice => notice.Changes == NotificationChange.Removed);
        Assert.Contains(notices, notice => notice.Changes == NotificationChange.Project);
    }
}
