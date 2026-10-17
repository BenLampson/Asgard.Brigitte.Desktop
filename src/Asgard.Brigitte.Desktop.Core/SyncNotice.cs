namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本地未读动态，仅保存项目归属、BUG 标题、变更摘要和时间，不复制正文或回复内容。</summary>
public sealed record SyncNotice(string Id, string Message, DateTimeOffset Time, bool Read);
