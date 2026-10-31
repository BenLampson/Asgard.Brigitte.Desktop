using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证结构化记录与提醒规则对真实同步提交的影响。</summary>
public sealed class StructuredChangesTests
{
    [Fact]
    public async Task Tray_keeps_bug_statistics_when_project_notifications_are_muted()
    {
        var folder = Directory.CreateTempSubdirectory("brigitte-tray-muted-");
        try
        {
            var preferences = new NotificationPreferenceStore(Path.Combine(folder.FullName, "notifications.json"));
            preferences.Set("render-test", "project", new NotificationRule { Enabled = false });
            var html = System.Net.WebUtility.HtmlDecode(await WindowRenderTests.Render("light", Asgard.Brigitte.Desktop.UI.DesktopViewKind.Tray, notifications: preferences));
            var start = html.IndexOf("aria-label=\"Brigitte，未解决 14，其中高优先级 14\"", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var end = html.IndexOf("</button>", start, StringComparison.Ordinal);
            var row = html[start..end];
            Assert.Contains("Brigitte", row);
            Assert.Contains("is-priority", row);
            Assert.DoesNotContain("—", row);
        }
        finally { folder.Delete(true); }
    }
    [Fact]
    public void Muted_events_never_accumulate_and_business_projection_still_updates()
    {
        var folder = Directory.CreateTempSubdirectory("brigitte-notice-pref-");
        try
        {
            var preferences = new NotificationPreferenceStore(Path.Combine(folder.FullName, "notifications.json"));
            preferences.Set("test", "p", new NotificationRule { Enabled = false });
            using var local = new SyncTestWorkspace(preferences);
            var project = SyncProtocol.Row("project", "p", new { id = "p", name = "项目" });
            SyncRow Bug(string title) => SyncProtocol.Row("bug", "b", new { id = "b", projectId = "p", title });
            local.Replace(project, Bug("旧标题"));
            local.Replace(project, Bug("新标题"));
            Assert.Empty(local.Database.Notices());
            Assert.Equal("新标题", Assert.Single(local.Database.Read<Bug>("bug")).Title);
            preferences.Set("test", "p", new NotificationRule());
            Assert.Empty(local.Database.Notices());
            local.Replace(project, Bug("再次更新"));
            Assert.Single(local.Database.Notices());
        }
        finally { folder.Delete(true); }
    }

    [Fact]
    public void Mixed_events_are_filtered_when_the_saved_rule_changes()
    {
        var folder = Directory.CreateTempSubdirectory("brigitte-notice-mixed-");
        try
        {
            var preferences = new NotificationPreferenceStore(Path.Combine(folder.FullName, "notifications.json"));
            using var local = new SyncTestWorkspace(preferences);
            var project = SyncProtocol.Row("project", "p", new { id = "p", name = "项目" });
            SyncRow Bug(string title, string status) => SyncProtocol.Row("bug", "b", new { id = "b", projectId = "p", title, status });
            local.Replace(project, Bug("标题", "waiting"));
            local.Replace(project, Bug("新标题", "processing"));
            var original = Assert.Single(local.Database.Notices());
            Assert.Contains("标题", original.Message);
            Assert.Contains("状态", original.Message);
            Assert.Equal(2, original.Parts.Length);
            preferences.Set("test", "p", new NotificationRule { Changes = NotificationChange.Status, Statuses = ["processing"] });
            var filtered = Assert.Single(local.Database.Notices());
            Assert.DoesNotContain("修改了标题", filtered.Message);
            Assert.Contains("状态", filtered.Message);
            Assert.Equal(NotificationChange.Status, filtered.Changes);
            using (ClientText.UseLanguage("en")) Assert.DoesNotContain("changed the title", filtered.DisplayMessage, StringComparison.OrdinalIgnoreCase);
            preferences.Set("test", "p", new NotificationRule { Changes = NotificationChange.Status, Statuses = ["closed"] });
            Assert.Empty(local.Database.Notices());
        }
        finally { folder.Delete(true); }
    }

    [Fact]
    public void Records_sort_newest_first_with_a_stable_tie_breaker()
    {
        using var local = new SyncTestWorkspace();
        var time = DateTimeOffset.Parse("2026-10-01T12:00:00Z");
        local.Replace(SyncProtocol.Row("bug", "b", new Bug { Id = "b", ProjectId = "p" }),
            SyncProtocol.Row("comment", "old", new BugComment { Id = "old", BugId = "b", CreateTime = time.AddDays(-1) }),
            SyncProtocol.Row("comment", "a", new BugComment { Id = "a", BugId = "b", CreateTime = time }),
            SyncProtocol.Row("comment", "z", new BugComment { Id = "z", BugId = "b", CreateTime = time }));
        Assert.Equal(["z", "a", "old"], new LocalWorkspaceReader(local.Database).Detail("b").Comments.Select(item => item.Id));
    }

    [Fact]
    public void Structured_report_preserves_compatibility_text_and_unknown_versions_fall_back()
    {
        var report = new BugProcessReport { Summary = "修复完成，待人工验证", Changes = "修改权限", Verification = "未执行线上测试" };
        report.Validate();
        var json = System.Text.Json.JsonSerializer.Serialize(report, SyncProtocol.Json);
        var parsed = BugProcessReport.FromJson(json);
        Assert.Equal("修改权限", parsed?.Changes);
        Assert.Contains("未执行线上测试", report.ToLegacyMarkdown());
        Assert.DoesNotContain("提交与部署", report.ToLegacyMarkdown());
        Assert.Null(BugProcessReport.FromJson("{\"version\":2,\"summary\":\"unknown\"}"));
        Assert.Null(BugProcessReport.FromJson("{broken"));
        Assert.Throws<ArgumentException>(() => new BugProcessReport().Validate());
    }
}
