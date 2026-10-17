namespace Asgard.Brigitte.Sync;

/// <summary>有序同步批次：begin、rows、commit；只有 commit 后记录才能对界面可见。</summary>
public sealed record SyncFrame
{
    public string Phase { get; init; } = "";
    public string BatchId { get; init; } = "";
    public string BaseCursor { get; init; } = "";
    public string Cursor { get; init; } = "";
    public int Total { get; init; }
    public bool Replay { get; init; }
    public SyncRow[] Rows { get; init; } = [];
    public string[] Receipts { get; init; } = [];
}
