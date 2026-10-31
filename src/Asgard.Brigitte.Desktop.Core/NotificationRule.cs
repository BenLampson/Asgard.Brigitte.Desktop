namespace Asgard.Brigitte.Desktop.Core;

/// <summary>默认保持现有全部提醒；目标状态为 null 表示全部，空数组表示不提醒任何状态变化。</summary>
public sealed record NotificationRule
{
    /// <summary>项目总开关，关闭时所有变更类型均保持静默。</summary>
    public bool Enabled { get; init; } = true;
    /// <summary>同一批次命中任一类型即可提醒。</summary>
    public NotificationChange Changes { get; init; } = NotificationChange.All;
    /// <summary>只筛选状态变化的目标状态，不影响新建、回复等其它类型。</summary>
    public string[]? Statuses { get; init; }

    /// <summary>所有类型均关闭或状态列表为空且只开启状态时，项目完全静默。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAnyEnabledChange => Enabled &&
        ((Changes & ~NotificationChange.Status) != 0 ||
         (Changes & NotificationChange.Status) != 0 && (Statuses is null || Statuses.Length > 0));

    /// <summary>混合变更按任一匹配处理，状态只能由明确的状态事件命中。</summary>
    public bool Allows(SyncNotice notice) => Enabled &&
        ((Changes & notice.Changes & ~NotificationChange.Status) != 0 ||
         (Changes & notice.Changes & NotificationChange.Status) != 0 &&
         (Statuses is null || notice.TargetStatuses.Intersect(Statuses, StringComparer.Ordinal).Any()));
}
