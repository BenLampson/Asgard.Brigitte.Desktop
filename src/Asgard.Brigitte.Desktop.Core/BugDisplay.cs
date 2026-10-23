namespace Asgard.Brigitte.Desktop.Core;
/// <summary>只依据实际字段显示，不推断真人或 AI。</summary>
public static class BugDisplay
{
    /// <summary>现有全部状态。</summary>
    private static readonly IReadOnlyDictionary<string, string> StatusKeys = new Dictionary<string, string>
    {
        ["waiting"] = "等待领取",
        ["confirmed"] = "已确认",
        ["reproducing"] = "复现中",
        ["triaged"] = "已分诊",
        ["assigned"] = "已指派",
        ["processing"] = "处理中",
        ["reopened"] = "重新打开",
        ["pending_deployment"] = "待部署",
        ["pending_regression"] = "待人工回归",
        ["human_verified"] = "人工回归通过",
        ["blocked"] = "阻塞中",
        ["need_info"] = "待补充信息",
        ["fixed"] = "历史已处理 · 部署待确认",
        ["regressed"] = "历史回归通过 · 验收待确认",
        ["closed"] = "已关闭",
        ["wont_fix"] = "不处理",
        ["duplicate"] = "重复 BUG",
        ["cannot_reproduce"] = "无法复现"
    };
    /// <summary>默认看板展示全部状态，结束状态也必须保留可见的泳道。</summary>
    public static readonly string[] BoardStatuses = StatusKeys.Keys.ToArray();
    /// <summary>交付、人工验收和历史待确认都仍需跟进，仅归档或明确终止后结束。</summary>
    public static bool IsUnresolved(string status) => status is not ("closed" or "wont_fix" or "duplicate" or "cannot_reproduce");
    /// <summary>批量修复只包含尚未完成修复的事项，不重复修改待交付与验收记录。</summary>
    public static bool NeedsRepair(string status) => status is "waiting" or "confirmed" or "reproducing" or "triaged" or
        "assigned" or "processing" or "blocked" or "need_info" or "reopened";
    /// <summary>紧急度使用文字标签。</summary>
    private static readonly IReadOnlyDictionary<string, string> PriorityKeys = new Dictionary<string, string>
    { ["critical"] = "紧急", ["high"] = "高", ["medium"] = "中", ["low"] = "低" };
    private static readonly IReadOnlyDictionary<string, string> EnglishStatuses = StatusKeys.ToDictionary(x => x.Key, x => ClientText.Translate(x.Value, "en"));
    private static readonly IReadOnlyDictionary<string, string> EnglishPriorities = PriorityKeys.ToDictionary(x => x.Key, x => ClientText.Translate(x.Value, "en"));
    /// <summary>两种语言各缓存一次标签，语言切换不创建每张卡片的重复字典。</summary>
    public static IReadOnlyDictionary<string, string> Statuses => ClientText.Language == "en" ? EnglishStatuses : StatusKeys;
    public static IReadOnlyDictionary<string, string> Priorities => ClientText.Language == "en" ? EnglishPriorities : PriorityKeys;
    /// <summary>目录未匹配保留原始标识，空值才显示未指派。</summary>
    public static string Assignee(string? id, IEnumerable<IdentityUser> users)
    {
        if (string.IsNullOrWhiteSpace(id)) return ClientText.T("未指派");
        var user = users.FirstOrDefault(x => x.Id == id);
        if (user is null) return id;
        var name = string.IsNullOrEmpty(user.DisplayName) ? user.Username : user.DisplayName;
        return !string.IsNullOrEmpty(user.Username) && user.Username != name ? $"{name}（{user.Username}）" : name;
    }
    /// <summary>领取人仅取已验证的用户并匹配目录；Worker 名称不能作为用户身份。</summary>
    public static string Claimant(Bug bug, IEnumerable<IdentityUser>? users = null) => !HasLock(bug) ? ClientText.T("未领取")
        : string.IsNullOrWhiteSpace(bug.LockOwnerUserId) ? ClientText.T("领取用户未知") : Assignee(bug.LockOwnerUserId, users ?? []);
    /// <summary>已过期的持锁标识不作为活跃领取者。</summary>
    public static bool HasLock(Bug bug) => !string.IsNullOrWhiteSpace(bug.LockOwnerId) && bug.LockExpireTime > DateTimeOffset.UtcNow;
    /// <summary>无锁或本客户端持锁时允许写入，服务器仍有最终决定权。</summary>
    public static bool CanWrite(Bug bug, string workerId, string? userId = null) => !HasLock(bug) || OwnsLock(bug, workerId, userId);
    /// <summary>共享设备切换账号后，相同 Worker 也不代表同一个领取人。</summary>
    public static bool OwnsLock(Bug bug, string workerId, string? userId) => !string.IsNullOrWhiteSpace(userId)
        && bug.LockOwnerId == workerId && bug.LockOwnerUserId == userId;
    /// <summary>短 ID 只用于显示，复制动作使用原值。</summary>
    public static string ShortId(string value) => value.Length > 8 ? value[..8] : value;
    /// <summary>本地过滤与刷新独立，不覆盖界面选择。</summary>
    public static bool Matches(Bug b, string keyword, string status, string priority, string assignee, string source)
        => (string.IsNullOrEmpty(keyword) || $"{b.Title} {b.Description} {b.Content} {b.Source} {b.SourceRef}".Contains(keyword, StringComparison.OrdinalIgnoreCase))
        && (status == "" || b.Status == status) && (priority == "" || b.Priority == priority)
        && (assignee == "" || (assignee == "__none" ? string.IsNullOrEmpty(b.AssigneeId) : b.AssigneeId == assignee))
        && (source == "" || b.Source == source);
}
