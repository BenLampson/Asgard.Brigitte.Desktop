namespace Asgard.Brigitte.Desktop.Core;

/// <summary>看板快捷流转复用既有接口，自动锁只属于本次操作。</summary>
public sealed partial class BrigitteApi
{
    /// <summary>说明和人工确认在调用前完成；领取后再以返回的最新状态复核，最后释放自动锁。</summary>
    public async Task<BugStatusMoveResult> MoveStatusAsync(string id, string expectedStatus, string status,
        string reason, string workerId, string? userId)
    {
        var identity = session.IdentityVersion;
        var current = (await DetailAsync(id)).Bug;
        if (current.Status != expectedStatus) throw new InvalidOperationException(ClientText.T("状态已变化，请刷新后重新拖动。"));
        if (current.Status == status) return new(false);
        BugWorkflow.Validate(current.Status, status, reason);
        if (!BugDisplay.CanWrite(current, workerId, userId))
            throw new InvalidOperationException(ClientText.T("其他 Worker 持锁期间无法修改。"));

        // 手工领取的锁仍由原窗口管理；独立标识避免释放并发窗口刚领取的锁。
        var automatic = !BugDisplay.HasLock(current);
        var owner = automatic ? "board-move-" + Guid.NewGuid().ToString("N") : workerId;
        var release = false;
        var changed = false;
        Exception? error = null, releaseError = null;
        try
        {
            RequireIdentity();
            if (automatic)
            {
                release = true;
                try { current = await ClaimAsync(id, owner, 1); }
                // 明确拒绝领取的响应不执行释放；断线或同步失败仍尝试清理本次独立锁。
                catch (HttpRequestException ex) when (ex.StatusCode is not null) { release = false; throw; }
            }
            else
            {
                // 本人手工锁也通过领取响应核对线上最新状态，保留剩余租期且不自动释放。
                var minutes = (int)Math.Clamp(Math.Ceiling((current.LockExpireTime!.Value - DateTimeOffset.UtcNow).TotalMinutes), 1, 1440);
                current = await ClaimAsync(id, owner, minutes);
            }
            RequireIdentity();
            if (current.Status != expectedStatus) throw new InvalidOperationException(ClientText.T("状态已变化，请刷新后重新拖动。"));
            BugWorkflow.Validate(current.Status, status, reason);
            await StatusAsync(id, status, reason, owner);
            changed = true;
        }
        catch (Exception ex) { error = ex; }
        finally
        {
            if (release)
            {
                try { RequireIdentity(); await ReleaseAsync(id, owner); }
                catch (Exception ex) { releaseError = ex; }
            }
        }
        return new(changed, error, releaseError);

        // 一个拖动操作不能跨账号或跨本机/在线工作区继续写入。
        void RequireIdentity()
        {
            if (identity != session.IdentityVersion)
                throw new OperationCanceledException(ClientText.T("会话已变化，已丢弃旧命令结果。"));
        }
    }
}
