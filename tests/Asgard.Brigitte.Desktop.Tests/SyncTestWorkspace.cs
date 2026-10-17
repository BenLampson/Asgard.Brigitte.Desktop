using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>通过真实同步帧填充临时 SQLite，不为产品加入本地业务写入口。</summary>
internal sealed class SyncTestWorkspace : IDisposable
{
    public string DirectoryPath { get; } = Directory.CreateTempSubdirectory("brigitte-sync-test-").FullName;
    public LocalWorkspaceDatabase Database { get; }
    public SyncTestWorkspace() { Database = new(DirectoryPath); Database.OpenAccount("test"); }
    public SyncFrame Begin(params SyncRow[] rows)
    {
        var previous = Database.Manifest();
        var frame = new SyncFrame { Phase = "begin", BatchId = Guid.NewGuid().ToString("N"), BaseCursor = SyncProtocol.Cursor(previous), Replay = !Database.Ready,
            Cursor = SyncProtocol.Cursor(rows.Select(x => new SyncStamp(x.Key, x.Hash))), Total = SyncProtocol.Difference(previous.ToDictionary(x => x.Key, x => x.Hash), rows).Length };
        Database.Apply(frame); return frame;
    }
    public void Replace(params SyncRow[] rows)
    {
        var delta = SyncProtocol.Difference(Database.Manifest().ToDictionary(x => x.Key, x => x.Hash), rows);
        var frame = Begin(rows);
        Database.Apply(frame with { Phase = "rows", Rows = delta }); Database.Apply(frame with { Phase = "commit" });
    }
    public void Dispose()
    {
        Database.Dispose();
        var path = Path.GetFullPath(DirectoryPath);
        if (path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("brigitte-sync-test-")) Directory.Delete(path, true);
    }
}
