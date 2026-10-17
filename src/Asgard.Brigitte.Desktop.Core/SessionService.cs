using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop.Core;
/// <summary>共享原生用户会话；不自动重新进入 SSO，不重放业务写请求。</summary>
public sealed class SessionService(
    IOptions<ClientOptions> options, IHttpClientFactory factory,
    ICredentialStore store, ILoginBrowser browser, TimeProvider? timeProvider = null) : IClientSession, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _loginGate = new(1, 1);
    private StoredSession? _current;
    private long _version;
    private long _identityVersion;
    private bool _restorePending, _persistPending;
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private CancellationTokenSource? _loginCancellation;
    /// <summary>令牌只在原生服务中保留。</summary>
    private HeimdallOAuthClient CreateClient() => new(options.Value.OAuthOptions(), httpClient: factory.CreateClient("oidc"), timeProvider: _time);
    /// <summary>登录及失效由所有窗口共享。</summary>
    public event Action? Changed;
    /// <summary>当前会话是否可用。</summary>
    public bool IsAuthenticated => _current is not null;
    /// <summary>版本不含凭据。</summary>
    public long Version => Interlocked.Read(ref _version);
    /// <summary>仅身份更换时变化，正常令牌轮换不清空项目上下文。</summary>
    public long IdentityVersion => Interlocked.Read(ref _identityVersion);
    /// <summary>网络暂不可用时保留安全存储，允许后台或手动重新恢复。</summary>
    public bool RestorePending => _restorePending;
    /// <summary>持久化失败可见且可重试，不丢弃已经轮换的新令牌。</summary>
    public string PersistenceWarning { get; private set; } = "";
    /// <summary>当前账号显示资料。</summary>
    public string DisplayName => _current?.DisplayName ?? "";
    /// <summary>已验证的用户标识，仅用于分隔本地数据库，不包含凭据。</summary>
    public string SubjectId => _current?.SubjectId ?? "";
    /// <summary>启动时只从安全存储恢复；每次重新验证轮换后的会话。</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_current is not null) return;
            _restorePending = true;
            var raw = await store.ReadAsync();
            if (raw is null) { _restorePending = false; return; }
            StoredSession? saved;
            try { saved = JsonSerializer.Deserialize<StoredSession>(raw); }
            catch (JsonException) { await ClearAsync(); return; }
            if (saved?.Tokens?.RefreshToken is not { Length: > 0 } refresh || string.IsNullOrEmpty(saved.SubjectId) || string.IsNullOrEmpty(saved.SessionId)) { await ClearAsync(); return; }
            using var client = CreateClient();
            var tokens = await client.RefreshTokenAsync(refresh, saved.SubjectId, saved.SessionId, cancellationToken);
            var candidate = Build(tokens, saved.DisplayName);
            await CommitAsync(candidate, true);
        }
        catch (HeimdallClientException ex) when (ex.Category == "invalid_grant") { await ClearAsync(); }
        finally { _gate.Release(); Changed?.Invoke(); }
    }
    /// <summary>登录事务只在当前操作内存中存在且只消费一次。</summary>
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ClientId) || string.IsNullOrWhiteSpace(options.Value.ApplicationId))
            throw new InvalidOperationException("尚未配置 Heimdall 原生客户端 ClientId / ApplicationId，请按客户端 README 登记回调。");
        await _loginGate.WaitAsync(cancellationToken);
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loginCancellation = pending;
        pending.CancelAfter(TimeSpan.FromMinutes(5));
        var version = Version;
        try
        {
            using var client = CreateClient();
            var transaction = await client.CreateLoginRequestAsync(cancellationToken: pending.Token);
            var callback = await browser.AuthorizeAsync(options.Value.RedirectUri, transaction.Url, pending.Token);
            var tokens = await client.ExchangeCodeAsync(callback, transaction, pending.Token);
            var candidate = Build(tokens, "");
            var profile = await client.GetUserInfoAsync(tokens.AccessToken, candidate.SubjectId, pending.Token);
            candidate.DisplayName = profile.TryGetProperty("name", out var name) ? name.GetString() ?? candidate.SubjectId : candidate.SubjectId;
            await _gate.WaitAsync(pending.Token);
            try
            {
                if (Version != version) throw new OperationCanceledException("会话已变化，请重新登录。");
                await CommitAsync(candidate, true);
            }
            finally { _gate.Release(); }
            Changed?.Invoke();
            await browser.CompleteAsync(true);
        }
        catch { await browser.CompleteAsync(false); throw; }
        finally { _loginCancellation = null; _loginGate.Release(); }
    }
    /// <summary>合并并发刷新，网络失败保留原始会话。</summary>
    public async Task<TokenLease> GetLeaseAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var saved = _current ?? throw new UnauthorizedAccessException("请先登录。");
            if (_persistPending) await PersistAsync();
            if (saved.ExpiresAt <= _time.GetUtcNow().AddSeconds(60))
            {
                if (string.IsNullOrEmpty(saved.Tokens.RefreshToken)) { await ClearAsync(); throw new UnauthorizedAccessException("登录已过期。"); }
                using var client = CreateClient();
                try
                {
                    var version = Version;
                    var tokens = await client.RefreshTokenAsync(saved.Tokens.RefreshToken, saved.SubjectId, saved.SessionId, cancellationToken);
                    if (Version != version) throw new OperationCanceledException("会话已变化。");
                    var candidate = Build(tokens, saved.DisplayName);
                    await CommitAsync(candidate, false);
                }
                catch (HeimdallClientException ex) when (ex.Category == "invalid_grant")
                { await ClearAsync(); throw new UnauthorizedAccessException("登录已失效，请重新登录。"); }
            }
            return new TokenLease(Version, _current!.Tokens.AccessToken) { IdentityVersion = IdentityVersion };
        }
        finally { _gate.Release(); }
    }
    /// <summary>由桌面生命周期驱动，托盘空闲时也提前续期；从不自动发起浏览器登录。</summary>
    public async Task MaintainAsync(CancellationToken cancellationToken = default)
    {
        if (_restorePending && !IsAuthenticated) await InitializeAsync(cancellationToken);
        else if (IsAuthenticated) await GetLeaseAsync(cancellationToken);
    }
    /// <summary>先接纳 SDK 已验证的新凭据；磁盘失败时也不能退回已作废的旧 RT。</summary>
    private async Task CommitAsync(StoredSession candidate, bool identityChanged)
    {
        _current = candidate; _restorePending = false; _persistPending = true;
        Interlocked.Increment(ref _version);
        if (identityChanged) Interlocked.Increment(ref _identityVersion);
        await PersistAsync();
    }
    /// <summary>保存轮换结果；短暂 I/O 失败由后台重试并向用户提示。</summary>
    private async Task PersistAsync()
    {
        var warning = PersistenceWarning;
        try { await store.WriteAsync(JsonSerializer.Serialize(_current)); _persistPending = false; PersistenceWarning = ""; }
        catch { PersistenceWarning = "登录信息暂时无法保存到本机，正在自动重试。保存成功前请保持客户端运行。"; }
        if (warning != PersistenceWarning) Changed?.Invoke();
    }
    /// <summary>只失效发起请求时捕获的会话。</summary>
    public async Task InvalidateAsync(long version)
    {
        await _gate.WaitAsync();
        try { if (Version == version) await ClearAsync(); }
        finally { _gate.Release(); }
    }
    /// <summary>普通退出仅撤销当前应用授权，不结束其它软件的 SSO。</summary>
    public async Task LogoutAsync()
    {
        _loginCancellation?.Cancel();
        await _gate.WaitAsync();
        StoredSession? old;
        try { old = _current; await ClearAsync(); }
        finally { _gate.Release(); }
        if (old is null) return;
        using var client = CreateClient();
        try
        {
            if (!string.IsNullOrEmpty(old.Tokens.RefreshToken)) await client.RevokeAsync(old.Tokens.RefreshToken, "refresh_token");
            await client.RevokeAsync(old.Tokens.AccessToken, "access_token");
        }
        catch (HttpRequestException) { /* 本机仍保持退出，绝不恢复旧凭据。 */ }
        catch (HeimdallClientException) { /* 远端未确认不影响本地门禁。 */ }
    }
    /// <summary>在持有会话锁时清空本地身份。</summary>
    private async Task ClearAsync()
    {
        _current = null;
        _restorePending = false; _persistPending = false; PersistenceWarning = "";
        Interlocked.Increment(ref _version);
        Interlocked.Increment(ref _identityVersion);
        Changed?.Invoke();
        await store.ClearAsync();
    }
    /// <summary>仅解析已由官方 SDK 验证过的 JWT，不将此解析用作验签。</summary>
    private static StoredSession Build(HeimdallTokenSet tokens, string displayName)
    {
        var part = tokens.AccessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        part = part.PadRight((part.Length + 3) / 4 * 4, '=');
        using var document = JsonDocument.Parse(Convert.FromBase64String(part));
        var claims = document.RootElement;
        return new StoredSession
        {
            Tokens = tokens,
            SubjectId = claims.GetProperty("sub").GetString()!,
            SessionId = claims.TryGetProperty("sid", out var sid) ? sid.GetString() ?? "" : "",
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(claims.GetProperty("exp").GetInt64()),
            DisplayName = displayName
        };
    }
    /// <summary>释放同步与取消资源。</summary>
    public void Dispose() { _loginCancellation?.Cancel(); _gate.Dispose(); _loginGate.Dispose(); }
}
