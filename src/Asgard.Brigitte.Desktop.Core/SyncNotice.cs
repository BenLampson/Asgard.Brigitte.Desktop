namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本地未读动态，仅保存项目归属、BUG 标题、变更摘要和时间，不复制正文或回复内容。</summary>
public sealed record SyncNotice(string Id, string Message, DateTimeOffset Time, bool Read)
{
    /// <summary>实时提醒的结构化归属与类型；历史未读正文仍沿用原存储格式。</summary>
    public string? ProjectId { get; init; }
    public NotificationChange Changes { get; init; }
    /// <summary>同批次可能包含多次状态转换，保留各次的目标状态。</summary>
    public string[] TargetStatuses { get; init; } = [];
    /// <summary>双语摘要与同一事件一起保存，切换语言不改写项目和人员原文。</summary>
    public string? EnglishMessage { get; init; }
    /// <summary>可独立筛选的变更及标题，旧版动态没有这些字段。</summary>
    public string? Subject { get; init; }
    public string? EnglishSubject { get; init; }
    public SyncNoticePart[] Parts { get; init; } = [];
    public string DisplayMessage => ClientText.Language == "en" ? EnglishMessage ?? Message : Message;
}

/// <summary>合并动态中的单项变更，保存其类型和状态以便规则变更后重新筛选。</summary>
public sealed record SyncNoticePart(string Message, NotificationChange Change, string? TargetStatus)
{
    public string? EnglishMessage { get; init; }
}
