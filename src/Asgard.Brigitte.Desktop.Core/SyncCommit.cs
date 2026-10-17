namespace Asgard.Brigitte.Desktop.Core;

/// <summary>只有 SQLite 事务提交完成后才产生的界面更新信号。</summary>
public sealed record SyncCommit(bool Replay, IReadOnlyList<SyncNotice> Notices, string[] Receipts);
