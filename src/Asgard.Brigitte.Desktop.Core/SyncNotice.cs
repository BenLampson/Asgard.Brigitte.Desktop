namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本地未读动态，仅保存项目归属、BUG 标题、变更摘要和时间，不复制正文或回复内容。</summary>
public sealed record SyncNotice(string Id, string Message, DateTimeOffset Time, bool Read)
{
    /// <summary>双语摘要与同一事件一起保存，切换语言不改写项目和人员原文。</summary>
    public string? EnglishMessage { get; init; }
    public string DisplayMessage => ClientText.Language == "en" ? EnglishMessage ?? Message : Message;
}
