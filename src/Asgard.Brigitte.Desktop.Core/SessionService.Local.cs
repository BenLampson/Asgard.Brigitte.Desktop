namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本机使用是进程内的工作区选择，不伪造在线账号或持久化登录凭据。</summary>
public sealed partial class SessionService
{
    private volatile bool _isLocal;
    /// <summary>每次启动保持未选择状态，由欢迎页显式进入本机工作区。</summary>
    public bool IsLocal => _isLocal;
    /// <summary>界面允许本机工作区和已认证的在线工作区分别进入。</summary>
    public bool HasWorkspace => IsLocal || IsAuthenticated;

    /// <summary>本地数据库成功打开后再调用；不读取安全存储或请求身份服务。</summary>
    public async Task UseLocalAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsAuthenticated) throw new InvalidOperationException(ClientText.T("请先退出在线工作区。"));
            _isLocal = true;
            _restorePending = false;
            Interlocked.Increment(ref _version);
            Interlocked.Increment(ref _identityVersion);
        }
        finally { _gate.Release(); }
        Changed?.Invoke();
    }

    /// <summary>返回欢迎页时保留本机数据和已有在线安全凭据，不发起远程撤销。</summary>
    private async Task LeaveLocalAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _isLocal = false;
            Interlocked.Increment(ref _version);
            Interlocked.Increment(ref _identityVersion);
        }
        finally { _gate.Release(); }
        Changed?.Invoke();
    }
}
