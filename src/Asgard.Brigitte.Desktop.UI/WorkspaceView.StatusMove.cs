using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;

namespace Asgard.Brigitte.Desktop.UI;

/// <summary>卡片落下才开始处理，收集说明或取消拖动不会领取处理锁。</summary>
public partial class WorkspaceView
{
    private BugCardDrop? _pendingMove;
    private string? _movingBugId;
    // 实时刷新只清理加载错误；命令或释放失败必须保留到用户下一次操作。
    private string _moveError = "";
    private long _moveIdentity;
    private bool _disposed;

    /// <summary>直接在卡片上声明当前允许的落点，锁住、只读和重复提交时不允许起拖。</summary>
    private string MoveTargets(Bug bug) => Host.CanWrite && _movingBugId is null && _pendingMove is null
        && BugDisplay.CanWrite(bug, Host.WorkerId, Workspace.Access.UserId)
        ? string.Join(',', BugDisplay.BoardStatuses.Where(s => s != bug.Status && BugWorkflow.CanTransition(bug.Status, s))) : "";

    private async Task MoveBug(BugCardDrop move)
    {
        if (_disposed || !Host.CanWrite || _pendingMove is not null || _movingBugId is not null) return;
        var bug = _bugs.FirstOrDefault(b => b.Id == move.BugId && b.ProjectId == _selected);
        if (bug is null || bug.Status != move.FromStatus || !MoveTargets(bug).Split(',').Contains(move.ToStatus)) return;
        _pendingMove = move;
        _moveIdentity = Session.IdentityVersion;
        _moveError = _toast = "";
        if (!BugWorkflow.RequiresReason(move.ToStatus)) await CompleteMove("");
    }

    /// <summary>必填说明和人工确认完成之后，才执行领取、流转和释放。</summary>
    private async Task CompleteMove(string reason)
    {
        if (_pendingMove is not { } move || _movingBugId is not null) return;
        var projectId = _selected;
        _movingBugId = move.BugId;
        try
        {
            if (move.ToStatus == "human_verified" && !await Host.ConfirmAsync(ClientText.T("人工回归通过"), ClientText.T("仅在你已完成实际人工验收后确认；AI 自测不属于人工验收。"))) return;
            if (_disposed || !Host.CanWrite || _moveIdentity != Session.IdentityVersion
                || !_bugs.Any(b => b.Id == move.BugId && b.ProjectId == _selected))
                throw new InvalidOperationException(ClientText.T("工作区已变化，请重新拖动。"));
            _pendingMove = null;
            var result = await Api.MoveStatusAsync(move.BugId, move.FromStatus, move.ToStatus, reason, Host.WorkerId, Workspace.Access.UserId);
            if (_disposed || _moveIdentity != Session.IdentityVersion) return;
            Host.PublishChange();
            await Reload();
            if (_selected != projectId) return;
            if (result.Changed) _toast = ClientText.T("已移至{0}", BugDisplay.Statuses[move.ToStatus]);
            if (result.Error is not null) _moveError = AppView.SafeError(result.Error);
            if (result.ReleaseError is not null)
                _moveError += (_moveError.Length > 0 ? "\n" : "") + ClientText.T("自动释放锁失败；本次临时锁最长 1 分钟后到期。{0}", AppView.SafeError(result.ReleaseError));
        }
        catch (Exception ex) { if (!_disposed && _moveIdentity == Session.IdentityVersion && _selected == projectId) _moveError = AppView.SafeError(ex); }
        finally { _movingBugId = null; _pendingMove = null; }
    }

    private void CancelMove() { if (_movingBugId is null) _pendingMove = null; }
}
