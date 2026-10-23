using System.Collections.Concurrent;
using Asgard.Brigitte.Sync;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop.Core;

/// <summary>进程共享一条同步流：收到具体变更后先提交 SQLite，再通知所有窗口。</summary>
public sealed class DesktopRealtime : IWorkspaceSync, IDisposable
{
    private readonly SessionService _session;
    private readonly ClientOptions _options;
    private readonly LocalWorkspaceDatabase _database;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _receipts = new();
    private Task? _running;
    private HubConnection? _connection;
    private long _identity = -1;
    private volatile bool _connected;
    public event Action? Changed;
    public event Action? StatusChanged;
    public event Action<IReadOnlyList<SyncNotice>>? Notification;
    private string _status = "未连接";
    public string Status => ClientText.T(_status);
    public bool Ready => _database.Ready;
    public bool CanWrite => _connected && Ready;
    public int Received => _database.Received;
    public int Total => _database.Total;
    public bool HasManifest { get; private set; }
    public int PendingCommands => _receipts.Count;
    public IReadOnlyList<SyncNotice> Notices => _database.Notices();
    public void MarkNoticesRead() => _database.MarkNoticesRead();
    public void MarkNoticeRead(string id) => _database.MarkNoticeRead(id);
    public DesktopRealtime(SessionService session, IOptions<ClientOptions> options, LocalWorkspaceDatabase database)
    {
        _session = session; _options = options.Value; _database = database;
        database.Committed += OnCommitted; session.Changed += IdentityChanged;
    }
    public void Start() => _running ??= RunAsync(_stopping.Token);
    private void IdentityChanged()
    {
        if (_identity == _session.IdentityVersion && _session.IsAuthenticated) return;
        _connected = false; HasManifest = false; _database.Close();
        foreach (var receipt in _receipts.Values) receipt.TrySetException(new OperationCanceledException(ClientText.T("会话已变化；线上已提交的操作不会重放。")));
        _receipts.Clear(); SetStatus(_session.IsAuthenticated ? "已登录，正在连接工作区" : "未登录");
    }
    private void OnCommitted(SyncCommit commit)
    {
        if (!_session.IsAuthenticated || _session.IdentityVersion != _identity) return;
        Changed?.Invoke();
        foreach (var receipt in commit.Receipts) if (_receipts.TryRemove(receipt, out var waiting)) waiting.TrySetResult();
        if (!commit.Replay && commit.Notices.Count > 0) Notification?.Invoke(commit.Notices);
    }
    /// <summary>成功命令只请求 WS 同步屏障；断线时保留等待，重连后只重发屏障，绝不重发命令。</summary>
    public async Task ConfirmAsync(CancellationToken token = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _receipts[id] = completion; StatusChanged?.Invoke();
        try
        {
            await SendReceiptAsync(id);
            // HTTP 已成功，不能因普通超时解锁创建按钮；只允许会话结束或显式取消终止等待。
            await completion.Task.WaitAsync(token);
        }
        finally { _receipts.TryRemove(id, out _); StatusChanged?.Invoke(); }
    }
    private async Task SendReceiptAsync(string id)
    {
        var connection = _connection;
        if (connection?.State != HubConnectionState.Connected) return;
        try { await connection.InvokeAsync("Checkpoint", id, _stopping.Token); }
        catch (Exception) { /* 重连后重新发送同步屏障；关闭进程时不再等待。 */ }
    }
    private async Task RunAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            if (!_session.IsAuthenticated)
            { SetStatus("未登录"); try { await Task.Delay(500, stopping); } catch (OperationCanceledException) { return; } continue; }
            var identity = _session.IdentityVersion; _identity = identity;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            void SessionChanged() { if (!_session.IsAuthenticated || identity != _session.IdentityVersion) lifetime.Cancel(); }
            _session.Changed += SessionChanged;
            var failure = "";
            try
            {
                _database.OpenAccount(_options.ApiBaseUrl.TrimEnd('/') + "\n" + _options.TenantId + "\n" + _session.SubjectId);
                SetStatus("正在连接工作区"); HasManifest = false;
                await using var connection = new HubConnectionBuilder().WithUrl(new Uri(new Uri(_options.ApiBaseUrl), "api/brigitte/sync"), o =>
                {
                    foreach (var header in DesktopClientIdentity.Headers(_options)) o.Headers[header.Key] = header.Value;
                    o.Transports = HttpTransportType.WebSockets; o.SkipNegotiation = true;
                    o.AccessTokenProvider = async () => (await _session.GetLeaseAsync(lifetime.Token)).AccessToken;
                }).Build();
                _connection = connection;
                await connection.StartAsync(lifetime.Token);
                foreach (var chunk in _database.Manifest().Chunk(64)) await connection.InvokeAsync("Manifest", SyncProtocol.Version, chunk, lifetime.Token);
                // 即使空库也校验协议版本。
                await connection.InvokeAsync("Manifest", SyncProtocol.Version, Array.Empty<SyncStamp>(), lifetime.Token);
                SetStatus("正在核对线上数据");
                var first = true;
                await foreach (var frame in connection.StreamAsync<SyncFrame>("Synchronize", lifetime.Token))
                {
                    if (!_session.IsAuthenticated || identity != _session.IdentityVersion) break;
                    _database.Apply(frame);
                    if (frame.Phase == "begin") HasManifest = true;
                    if (frame.Phase == "denied") { SetStatus("访问权限已失效，请重新登录"); await _session.InvalidateAsync(_session.Version); break; }
                    if (frame.Phase == "commit")
                    {
                        _connected = true; SetStatus("已同步");
                        // 不能在消费流的线程等待 Hub 调用响应，避免大批次背压阻塞响应帧。
                        if (first) { first = false; foreach (var id in _receipts.Keys) _ = SendReceiptAsync(id); }
                    }
                    else SetStatus("正在同步数据");
                    StatusChanged?.Invoke();
                }
            }
            catch (Exception error)
            {
                if (stopping.IsCancellationRequested) return;
                // 安全分类不包含凭据、服务地址或业务正文，存储失败不能误报为网络失败。
                failure = error switch
                {
                    Microsoft.Data.Sqlite.SqliteException => "本地保存失败，请检查磁盘空间和文件权限；正在重试",
                    InvalidDataException => "同步校验失败，正在重新对账",
                    Microsoft.AspNetCore.SignalR.HubException => "服务端同步失败，正在自动重试",
                    _ => Ready ? "连接中断，正在重连；现有内容只读" : "连接中断，正在重新连接工作区"
                };
            }
            finally { _connected = false; _connection = null; HasManifest = false; _database.AbortBatch(); _session.Changed -= SessionChanged; }
            SetStatus(_session.IsAuthenticated ? failure.Length > 0 ? failure : "正在重新连接工作区" : "未登录");
            try { await Task.Delay(3000, stopping); } catch (OperationCanceledException) { return; }
        }
    }
    private void SetStatus(string status) { _status = status; StatusChanged?.Invoke(); }
    public void Dispose()
    {
        _stopping.Cancel(); _session.Changed -= IdentityChanged; _database.Committed -= OnCommitted;
        foreach (var receipt in _receipts.Values) receipt.TrySetCanceled();
    }
}
