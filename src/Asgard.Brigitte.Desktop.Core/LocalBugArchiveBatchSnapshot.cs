namespace Asgard.Brigitte.Desktop.Core;

/// <summary>同项目内按固定顺序读取的全部本机 BUG。</summary>
internal sealed record LocalBugArchiveBatchSnapshot(string ProjectId, LocalBugArchiveSnapshot[] Bugs);
