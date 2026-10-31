using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证本机规则继承、目标状态、重启保存和账号隔离；所有文件均位于专用临时目录。</summary>
public sealed class NotificationPreferenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("brigitte-notifications-test-").FullName;
    private string ConfigPath => Path.Combine(_directory, "notifications.json");
    private const string Account = "https://service.test\ntenant\nuser";
    private static SyncNotice Notice(NotificationChange type, string project = "p", params string[] statuses) =>
        new("notice", "无需解析正文", DateTimeOffset.UnixEpoch, false) { ProjectId = project, Changes = type, TargetStatuses = statuses };

    /// <summary>首次使用完整保留原有所有类型和状态的提醒。</summary>
    [Theory]
    [InlineData(NotificationChange.Created)]
    [InlineData(NotificationChange.Status)]
    [InlineData(NotificationChange.Assignment)]
    [InlineData(NotificationChange.Details)]
    [InlineData(NotificationChange.Comments)]
    [InlineData(NotificationChange.Attachments)]
    [InlineData(NotificationChange.Project)]
    [InlineData(NotificationChange.Removed)]
    public void Defaults_keep_existing_notifications(NotificationChange type)
    {
        var store = new NotificationPreferenceStore(ConfigPath);
        Assert.True(store.Allows(Account, Notice(type, statuses: "pending_regression")));
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>领导可关闭默认，只对选定项目开启指定的状态变化；恢复继承后立即跟随默认。</summary>
    [Fact]
    public void Defaults_and_project_overrides_survive_restart_and_reset_to_inheritance()
    {
        var store = new NotificationPreferenceStore(ConfigPath);
        store.Set(Account, null, new() { Enabled = false });
        store.Set(Account, "followed", new() { Changes = NotificationChange.Status, Statuses = ["pending_regression", "closed"] });
        var restarted = new NotificationPreferenceStore(ConfigPath);
        Assert.False(restarted.Allows(Account, Notice(NotificationChange.Created, "new-project")));
        Assert.True(restarted.Allows(Account, Notice(NotificationChange.Status, "followed", "pending_regression")));
        Assert.False(restarted.Allows(Account, Notice(NotificationChange.Status, "followed", "processing")));
        Assert.False(restarted.Allows(Account, Notice(NotificationChange.Comments, "followed")));
        Assert.True(restarted.HasOverride(Account, "followed"));
        restarted.Set(Account, "followed", null);
        Assert.False(restarted.HasOverride(Account, "followed"));
        Assert.False(restarted.Allows(Account, Notice(NotificationChange.Status, "followed", "closed")));
        restarted.Set(Account, null, new());
        Assert.True(restarted.Allows(Account, Notice(NotificationChange.Comments, "followed")));
        Assert.False(File.Exists(ConfigPath + ".tmp"));
    }

    /// <summary>单项目静默不会连带关闭其他项目，也不会覆盖以后对默认规则的修改。</summary>
    [Fact]
    public void Muting_one_project_does_not_mute_other_projects()
    {
        var store = new NotificationPreferenceStore(ConfigPath);
        store.Set(Account, "quiet", new() { Enabled = false });
        Assert.False(store.Allows(Account, Notice(NotificationChange.Status | NotificationChange.Comments, "quiet", "pending_regression")));
        Assert.True(store.Allows(Account, Notice(NotificationChange.Created, "other")));
        store.Set(Account, null, new() { Changes = NotificationChange.Status, Statuses = ["closed"] });
        Assert.False(store.Allows(Account, Notice(NotificationChange.Status, "quiet", "closed")));
        Assert.True(store.Allows(Account, Notice(NotificationChange.Status, "other", "closed")));
    }

    /// <summary>账号、租户及服务边界任意变化时，不继承另一个身份的项目设置。</summary>
    [Theory]
    [InlineData("https://other.test\ntenant\nuser")]
    [InlineData("https://service.test\nother\nuser")]
    [InlineData("https://service.test\ntenant\nother")]
    public void Different_accounts_do_not_share_rules(string other)
    {
        var store = new NotificationPreferenceStore(ConfigPath);
        store.Set(Account, null, new() { Enabled = false });
        store.Set(Account, "p", new() { Enabled = false });
        var restarted = new NotificationPreferenceStore(ConfigPath);
        Assert.True(restarted.Allows(other, Notice(NotificationChange.Created)));
        Assert.False(restarted.HasOverride(other, "p"));
        Assert.DoesNotContain("https://service.test", File.ReadAllText(ConfigPath));
    }

    /// <summary>批次中多个目标状态命中任一即可；回复等事件单独筛选，不受状态选项误伤。</summary>
    [Fact]
    public void Status_filters_match_destinations_and_mixed_batches_match_any_selected_type()
    {
        var rule = new NotificationRule { Changes = NotificationChange.Status, Statuses = ["pending_regression"] };
        Assert.True(rule.Allows(Notice(NotificationChange.Status, "p", "processing", "pending_regression")));
        Assert.False(rule.Allows(Notice(NotificationChange.Status, "p", "closed")));
        Assert.False(rule.Allows(Notice(NotificationChange.Comments, "p", "pending_regression")));
        Assert.False(rule.Allows(Notice(NotificationChange.Status | NotificationChange.Comments, "p", "processing")));
        Assert.True((rule with { Changes = NotificationChange.Status | NotificationChange.Comments }).Allows(
            Notice(NotificationChange.Status | NotificationChange.Comments, "p", "processing")));
        Assert.False((rule with { Statuses = [] }).Allows(Notice(NotificationChange.Status, "p", "pending_regression")));
        Assert.True((rule with { Statuses = null }).Allows(Notice(NotificationChange.Status, "p", "future-status")));
        Assert.False((rule with { Changes = NotificationChange.None }).Allows(Notice(NotificationChange.Status, "p", "pending_regression")));
    }

    /// <summary>读写草稿中的数组不能偷偷改变已保存规则。</summary>
    [Fact]
    public void Editing_rule_arrays_does_not_change_saved_preferences()
    {
        var store = new NotificationPreferenceStore(ConfigPath);
        var draft = new NotificationRule { Statuses = ["pending_regression"] };
        store.Set(Account, "p", draft);
        draft.Statuses![0] = "processing";
        store.Get(Account, "p").Statuses![0] = "closed";
        Assert.Equal(["pending_regression"], store.Get(Account, "p").Statuses!);
        store.Set(Account, null, new() { Statuses = [] });
        Assert.Empty(new NotificationPreferenceStore(ConfigPath).Get(Account).Statuses!);
    }

    [Fact]
    public void Retired_status_filters_do_not_enable_additional_notifications()
    {
        var store = new NotificationPreferenceStore(ConfigPath);
        store.Set(Account, "p", new NotificationRule { Changes = NotificationChange.Status, Statuses = ["fixed", "regressed"] });
        var rule = store.Get(Account, "p");
        Assert.Empty(rule.Statuses!);
        Assert.False(rule.Allows(Notice(NotificationChange.Status, "p", "pending_regression")));
    }

    /// <summary>文件写失败不能显示为成功，也不能改变内存中的实际生效规则。</summary>
    [Fact]
    public void Failed_save_preserves_previous_rules_in_memory_and_on_disk()
    {
        var store = new NotificationPreferenceStore(ConfigPath);
        store.Set(Account, null, new() { Enabled = false });
        Directory.CreateDirectory(ConfigPath + ".tmp");
        var failure = Record.Exception(() => store.Set(Account, null, new()));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.False(store.Get(Account).Enabled);
        Assert.False(new NotificationPreferenceStore(ConfigPath).Get(Account).Enabled);
    }

    /// <summary>损坏或旧格式的偏好不应阻断客户端启动。</summary>
    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{\"unused\":null}")]
    public void Invalid_configuration_keeps_safe_startup_defaults(string json)
    {
        File.WriteAllText(ConfigPath, json);
        Assert.True(new NotificationPreferenceStore(ConfigPath).Get(Account).Enabled);
    }

    /// <summary>递归清理前确认目标始终在专用系统临时目录之内。</summary>
    public void Dispose()
    {
        var resolved = Path.GetFullPath(_directory);
        if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(resolved).StartsWith("brigitte-notifications-test-")) Directory.Delete(resolved, true);
    }
}
