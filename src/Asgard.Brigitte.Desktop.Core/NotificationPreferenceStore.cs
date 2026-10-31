using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本机提醒偏好按服务、租户和账号隔离；未单独配置的项目实时继承默认规则。</summary>
public sealed class NotificationPreferenceStore
{
    private readonly string _path;
    private readonly LocalPreferenceDatabase? _database;
    private Dictionary<string, NotificationRule> _rules = [];

    /// <summary>配置不可读时保留原有全部提醒，不阻断客户端启动。</summary>
    public NotificationPreferenceStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(path))
                _rules = (JsonSerializer.Deserialize<Dictionary<string, NotificationRule?>>(File.ReadAllText(path)) ?? [])
                    .Where(x => x.Value is not null).ToDictionary(x => x.Key, x => Copy(x.Value!));
        }
        catch (JsonException) { /* 损坏的本地配置不影响同步和启动。 */ }
        catch (IOException) { /* 暂时不可读时使用默认提醒。 */ }
        catch (UnauthorizedAccessException) { /* 无读取权限时使用默认提醒。 */ }
    }
    /// <summary>从旧文件迁移提醒规则，SQLite 内继续按账号和项目隔离。</summary>
    public NotificationPreferenceStore(LocalPreferenceDatabase database, string legacyPath)
    {
        _path = legacyPath; _database = database;
        try
        {
            database.ImportLegacy("notifications", legacyPath, raw =>
                (JsonSerializer.Deserialize<Dictionary<string, NotificationRule?>>(raw) ?? [])
                .Where(item => item.Value is not null)
                .ToDictionary(item => "notifications/" + item.Key, item => JsonSerializer.Serialize(Copy(item.Value!))));
        }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Microsoft.Data.Sqlite.SqliteException) { }
        try
        {
            foreach (var item in database.ReadPrefix("notifications/"))
            {
                try { if (JsonSerializer.Deserialize<NotificationRule>(item.Value) is { } rule) _rules[item.Key[14..]] = Copy(rule); }
                catch (JsonException) { /* 单条规则损坏不影响其他项目。 */ }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Microsoft.Data.Sqlite.SqliteException) { }
    }

    /// <summary>返回独立副本，调用方不能绕过保存过程修改规则。</summary>
    public NotificationRule Get(string account, string? projectId = null) =>
        Copy(_rules.GetValueOrDefault(Key(account, projectId)) ??
             _rules.GetValueOrDefault(Key(account, null)) ?? new());

    /// <summary>跟随默认规则与显式项目规则在界面上分别呈现。</summary>
    public bool HasOverride(string account, string projectId) => _rules.ContainsKey(Key(account, projectId));

    /// <summary>传 null 恢复继承；先原子保存，失败时内存仍保持原规则。</summary>
    public void Set(string account, string? projectId, NotificationRule? rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        if (rule is not null && (rule.Changes & ~NotificationChange.All) != 0)
            throw new ArgumentException("提醒类型无效。", nameof(rule));
        var updated = new Dictionary<string, NotificationRule>(_rules);
        var key = Key(account, projectId);
        if (rule is null) updated.Remove(key);
        else updated[key] = Copy(rule);
        if (_database is not null)
        {
            if (rule is null) _database.Remove("notifications/" + key);
            else _database.Set("notifications/" + key, JsonSerializer.Serialize(updated[key]));
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated));
            File.Move(temporary, _path, true);
        }
        _rules = updated;
    }

    /// <summary>同一规则作用于弹窗与未读动态。</summary>
    public bool Allows(string account, SyncNotice notice) => Get(account, notice.ProjectId).Allows(notice);
    /// <summary>无法确定历史动态归属时，避免绕过账号下任一更严格的项目规则。</summary>
    public bool HasRestrictions(string account) => _rules.Any(pair => pair.Key.StartsWith(SyncProtocol.Hash(account) + "/", StringComparison.Ordinal)
        && (!pair.Value.Enabled || pair.Value.Changes != NotificationChange.All || pair.Value.Statuses is not null));
    /// <summary>账号键只记录摘要，不在偏好文件复制服务地址或用户身份。</summary>
    private static string Key(string account, string? projectId) => SyncProtocol.Hash(account) +
        (projectId is null ? "/default" : "/project/" + projectId);
    /// <summary>数组必须复制，避免编辑草稿影响尚未保存的有效配置。</summary>
    private static NotificationRule Copy(NotificationRule rule) => rule with
    {
        // 已退役的状态不可再筛选；空列表仍表示不提醒，不能扩大用户的提醒范围。
        Statuses = rule.Statuses?.Where(status => status is not ("fixed" or "regressed")).Distinct(StringComparer.Ordinal).ToArray()
    };
}
