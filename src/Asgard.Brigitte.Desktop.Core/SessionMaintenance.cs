namespace Asgard.Brigitte.Desktop.Core;
/// <summary>应用存活期间持续维护会话，隐藏托盘不停止；退出程序时取消。</summary>
public sealed class SessionMaintenance(SessionService session, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private Task? _running;
    /// <summary>只启动一份循环，多窗口不会重复轮换刷新令牌。</summary>
    public void Start() { _running ??= RunAsync(_stopping.Token); }
    /// <summary>每 30 秒检查一次，实际仅在令牌剩余不到一分钟时兑换。</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await session.MaintainAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch { /* 网络或服务短暂不可用保留原会话，下一轮重试，不自动打开 SSO。 */ }
            try { await Task.Delay(TimeSpan.FromSeconds(30), _time, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        }
    }
    /// <summary>停止维护；磁盘中的加密会话仍保留供下次启动恢复。</summary>
    public void Dispose() { _stopping.Cancel(); }
}
