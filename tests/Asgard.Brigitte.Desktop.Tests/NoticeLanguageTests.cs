using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>未读摘要的两种语言一起落库，重启后仍可切换，项目和用户名称始终保持原文。</summary>
public sealed class NoticeLanguageTests
{
    /// <summary>通知使用可信用户及目录名称，不能把 Worker 自报名称当成用户。</summary>
    [Fact]
    public void Claim_notice_uses_verified_user_and_legacy_claim_is_unknown()
    {
        using var fixture = new SyncTestWorkspace();
        var user = SyncProtocol.Row("user", "user-a", new IdentityUser { Id = "user-a", DisplayName = "真实领取者" });
        SyncRow Bug(string? worker, string? owner) => SyncProtocol.Row("bug", "b", new Bug
        { Id = "b", Title = "身份检查", LockOwnerId = worker, LockOwnerUserId = owner, LockOwnerName = "AI自报管理员" });
        fixture.Replace(user, Bug(null, null));
        fixture.Replace(user, Bug("worker", "user-a"));
        var claim = Assert.Single(fixture.Database.Notices());
        Assert.Contains("真实领取者", claim.Message); Assert.DoesNotContain("AI自报管理员", claim.Message);
        fixture.Database.MarkNoticesRead();
        fixture.Replace(user, Bug("legacy-worker", null));
        Assert.Contains("领取用户未知", Assert.Single(fixture.Database.Notices()).Message);
    }

    [Fact]
    public void Persisted_notice_switches_language_after_reopen_without_translating_user_content()
    {
        using var fixture = new SyncTestWorkspace();
        var project = SyncProtocol.Row("project", "p", new Project { Id = "p", Name = "中文项目" });
        SyncRow Bug(string status) => SyncProtocol.Row("bug", "b", new Bug { Id = "b", ProjectId = "p", Title = "保留原始标题", Status = status });
        fixture.Replace(project, Bug("waiting"));
        fixture.Replace(project, Bug("processing"));
        fixture.Database.Close(); fixture.Database.OpenAccount("test");
        fixture.Replace(project, Bug("processing"));
        var notice = Assert.Single(fixture.Database.Notices());
        using (ClientText.UseLanguage("en"))
        {
            Assert.Contains("In progress", notice.DisplayMessage);
            Assert.Contains("changed the status from", notice.DisplayMessage);
            Assert.Contains("中文项目", notice.DisplayMessage);
            Assert.Contains("保留原始标题", notice.DisplayMessage);
            Assert.DoesNotContain("将状态", notice.DisplayMessage);
        }
        using (ClientText.UseLanguage("zh-CN")) Assert.Contains("处理中", notice.DisplayMessage);
    }
}
