namespace Asgard.Brigitte.Desktop.Core;

/// <summary>在线命令与同步屏障，不能向 SQLite 写业务数据。</summary>
public interface IWorkspaceSync
{
    bool CanWrite { get; }
    Task ConfirmAsync(CancellationToken token = default);
}
