namespace Asgard.Brigitte.Desktop.Core;

/// <summary>区分状态提交和自动锁清理，释放失败不能伪装成状态未提交。</summary>
public sealed record BugStatusMoveResult(bool Changed, Exception? Error = null, Exception? ReleaseError = null);
