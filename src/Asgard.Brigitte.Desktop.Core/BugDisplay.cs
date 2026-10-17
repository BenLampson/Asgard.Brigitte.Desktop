namespace Asgard.Brigitte.Desktop.Core;
/// <summary>只依据实际字段显示，不推断真人或 AI。</summary>
public static class BugDisplay
{
    /// <summary>现有全部状态。</summary>
    public static readonly IReadOnlyDictionary<string, string> Statuses = new Dictionary<string, string>
    {
        ["waiting"] = "等待领取",
        ["confirmed"] = "已确认",
        ["reproducing"] = "复现中",
        ["triaged"] = "已分诊",
        ["assigned"] = "已指派",
        ["processing"] = "处理中",
        ["blocked"] = "阻塞中",
        ["need_info"] = "待补充信息",
        ["fixed"] = "已处理",
        ["regressed"] = "已回归",
        ["reopened"] = "重新打开",
        ["closed"] = "已关闭",
        ["wont_fix"] = "不处理",
        ["duplicate"] = "重复 BUG",
        ["cannot_reproduce"] = "无法复现"
    };
    /// <summary>网页端既有的 11 个泳道，不增加拖拽流转。</summary>
    public static readonly string[] BoardStatuses = ["waiting", "confirmed", "reproducing", "triaged", "assigned", "processing", "blocked", "need_info", "fixed", "regressed", "reopened"];
    /// <summary>仍需要处理的状态；已处理、已回归及其他结束状态不计入未解决。</summary>
    public static bool IsUnresolved(string status) => status is "waiting" or "confirmed" or "reproducing" or "triaged" or "assigned" or "processing" or "blocked" or "need_info" or "reopened";
    /// <summary>紧急度使用文字标签。</summary>
    public static readonly IReadOnlyDictionary<string, string> Priorities = new Dictionary<string, string>
    { ["critical"] = "紧急", ["high"] = "高", ["medium"] = "中", ["low"] = "低" };
    /// <summary>目录未匹配保留原始标识，空值才显示未指派。</summary>
    public static string Assignee(string? id, IEnumerable<IdentityUser> users)
    {
        if (string.IsNullOrWhiteSpace(id)) return "未指派";
        var user = users.FirstOrDefault(x => x.Id == id);
        if (user is null) return id;
        var name = string.IsNullOrEmpty(user.DisplayName) ? user.Username : user.DisplayName;
        return !string.IsNullOrEmpty(user.Username) && user.Username != name ? $"{name}（{user.Username}）" : name;
    }
    /// <summary>领取者与负责人独立，不要求用户账号。</summary>
    public static string Claimant(Bug bug) => HasLock(bug) ? string.IsNullOrWhiteSpace(bug.LockOwnerName) ? bug.LockOwnerId! : bug.LockOwnerName : "未领取";
    /// <summary>已过期的持锁标识不作为活跃领取者。</summary>
    public static bool HasLock(Bug bug) => !string.IsNullOrWhiteSpace(bug.LockOwnerId) && bug.LockExpireTime > DateTimeOffset.UtcNow;
    /// <summary>无锁或本客户端持锁时允许写入，服务器仍有最终决定权。</summary>
    public static bool CanWrite(Bug bug, string workerId) => !HasLock(bug) || bug.LockOwnerId == workerId;
    /// <summary>短 ID 只用于显示，复制动作使用原值。</summary>
    public static string ShortId(string value) => value.Length > 8 ? value[..8] : value;
    /// <summary>本地过滤与刷新独立，不覆盖界面选择。</summary>
    public static bool Matches(Bug b, string keyword, string status, string priority, string assignee, string source)
        => (string.IsNullOrEmpty(keyword) || $"{b.Title} {b.Description} {b.Content} {b.Source} {b.SourceRef}".Contains(keyword, StringComparison.OrdinalIgnoreCase))
        && (status == "" || b.Status == status) && (priority == "" || b.Priority == priority)
        && (assignee == "" || (assignee == "__none" ? string.IsNullOrEmpty(b.AssigneeId) : b.AssigneeId == assignee))
        && (source == "" || b.Source == source);
}
