using System.Text.Json;
using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>覆盖重新启动、轮换保存、并发续期、断网保留和明确失效。</summary>
public sealed class SessionPersistenceTests
{
    [Fact]
    public async Task Successful_write_is_not_discarded_or_replayed_when_background_rotates_tokens()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() }; using var session = server.Session(store);
        await session.InitializeAsync(TestContext.Current.CancellationToken);
        using var transport = new TestTransport(async _ => { clock.Advance(TimeSpan.FromMinutes(14.5)); await session.MaintainAsync(TestContext.Current.CancellationToken); return TestTransport.Json("{\"code\":200,\"data\":{\"saved\":true}}"); });
        var api = TestWorkspaceSync.Api(transport, session);
        var result = await api.WriteAsync<JsonElement>("bugs", new { title = "existing write" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.GetProperty("saved").GetBoolean()); Assert.Equal(1, transport.Count); Assert.Equal(2, server.Refreshes);
    }
    [Fact]
    public async Task Restart_uses_saved_rotated_refresh_without_browser_login()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() };
        using (var first = server.Session(store)) { await first.InitializeAsync(TestContext.Current.CancellationToken); Assert.True(first.IsAuthenticated); Assert.Equal("保存的账号", first.DisplayName); }
        using (var restarted = server.Session(store)) { await restarted.InitializeAsync(TestContext.Current.CancellationToken); Assert.True(restarted.IsAuthenticated); }
        Assert.Equal(new[] { "initial-refresh", "rotated-1" }, server.PresentedRefreshTokens); Assert.Equal("rotated-2", JsonSerializer.Deserialize<StoredSession>(store.Value!)!.Tokens.RefreshToken); Assert.Equal(0, store.BrowserOpens);
    }
    [Fact]
    public async Task Parallel_callers_share_one_renewal_and_old_401_cannot_remove_it()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() }; using var session = server.Session(store);
        await session.InitializeAsync(TestContext.Current.CancellationToken); var old = await session.GetLeaseAsync(TestContext.Current.CancellationToken); var identity = session.IdentityVersion; clock.Advance(TimeSpan.FromMinutes(14.5));
        var leases = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => session.GetLeaseAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(2, server.Refreshes); Assert.All(leases, l => Assert.Equal(leases[0].AccessToken, l.AccessToken)); Assert.NotEqual(old.Version, leases[0].Version); Assert.Equal(identity, session.IdentityVersion);
        await session.InvalidateAsync(old.Version); Assert.True(session.IsAuthenticated);
    }
    [Fact]
    public async Task Offline_start_retains_credentials_and_background_restores_after_network_returns()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var saved = server.Saved(); var store = new MemoryCredentialStore { Value = saved }; using var session = server.Session(store); server.Offline = true;
        var error = await Assert.ThrowsAsync<Asgard.Heimdall.Client.HeimdallClientException>(() => session.InitializeAsync(TestContext.Current.CancellationToken)); Assert.Equal("discovery_unavailable", error.Category); Assert.True(session.RestorePending); Assert.False(session.IsAuthenticated); Assert.Equal(saved, store.Value); Assert.Equal(0, store.Clears);
        server.Offline = false; await session.MaintainAsync(TestContext.Current.CancellationToken); Assert.True(session.IsAuthenticated); Assert.False(session.RestorePending); Assert.Equal(0, store.BrowserOpens);
    }
    [Fact]
    public async Task Temporary_refresh_failure_preserves_login_and_retries_same_grant()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() }; using var session = server.Session(store); await session.InitializeAsync(TestContext.Current.CancellationToken); var saved = store.Value; clock.Advance(TimeSpan.FromMinutes(14.5)); server.Offline = true;
        var error = await Assert.ThrowsAsync<Asgard.Heimdall.Client.HeimdallClientException>(() => session.MaintainAsync(TestContext.Current.CancellationToken)); Assert.Equal("discovery_unavailable", error.Category); Assert.True(session.IsAuthenticated); Assert.Equal(saved, store.Value);
        server.Offline = false; await session.MaintainAsync(TestContext.Current.CancellationToken); Assert.Equal("rotated-1", server.PresentedRefreshTokens[^1]); Assert.Equal(0, store.BrowserOpens);
    }
    [Fact]
    public async Task Invalid_grant_clears_local_login_and_does_not_auto_open_SSO()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() }; using var session = server.Session(store); await session.InitializeAsync(TestContext.Current.CancellationToken); clock.Advance(TimeSpan.FromMinutes(14.5)); server.InvalidGrant = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => session.MaintainAsync(TestContext.Current.CancellationToken)); Assert.False(session.IsAuthenticated); Assert.Null(store.Value); await session.MaintainAsync(TestContext.Current.CancellationToken); Assert.Equal(0, store.BrowserOpens);
    }
    [Fact]
    public async Task Failed_disk_write_keeps_new_refresh_token_in_memory_and_retries_persistence()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() }; using var session = server.Session(store); await session.InitializeAsync(TestContext.Current.CancellationToken); clock.Advance(TimeSpan.FromMinutes(14.5)); store.FailWrites = true;
        await session.MaintainAsync(TestContext.Current.CancellationToken); Assert.True(session.IsAuthenticated); Assert.NotEmpty(session.PersistenceWarning); Assert.Equal("rotated-1", JsonSerializer.Deserialize<StoredSession>(store.Value!)!.Tokens.RefreshToken);
        store.FailWrites = false; await session.MaintainAsync(TestContext.Current.CancellationToken); Assert.Equal("", session.PersistenceWarning); Assert.Equal("rotated-2", JsonSerializer.Deserialize<StoredSession>(store.Value!)!.Tokens.RefreshToken); Assert.Equal(2, server.Refreshes);
    }
    [Fact]
    public async Task Idle_background_worker_renews_without_business_requests()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() }; using var session = server.Session(store); await session.InitializeAsync(TestContext.Current.CancellationToken); clock.Advance(TimeSpan.FromMinutes(14.5)); using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); using var maintenance = new SessionMaintenance(session, clock);
        var running = maintenance.RunAsync(stopping.Token); await server.Renewed.Task.WaitAsync(TestContext.Current.CancellationToken); stopping.Cancel(); await running;
        Assert.Equal(2, server.Refreshes); Assert.Equal("rotated-2", JsonSerializer.Deserialize<StoredSession>(store.Value!)!.Tokens.RefreshToken); Assert.Equal(0, store.BrowserOpens);
    }
    [Fact]
    public async Task Explicit_logout_removes_saved_login_for_next_launch()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = server.Saved() }; using (var session = server.Session(store)) { await session.InitializeAsync(TestContext.Current.CancellationToken); await session.LogoutAsync(); }
        using var restarted = server.Session(store); await restarted.InitializeAsync(TestContext.Current.CancellationToken); Assert.False(restarted.IsAuthenticated); Assert.Null(store.Value); Assert.Equal(1, server.Refreshes);
    }
    [Fact]
    public async Task Corrupted_saved_JSON_is_removed_instead_of_repeated_browser_login()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock); var store = new MemoryCredentialStore { Value = "{broken" }; using var session = server.Session(store); await session.InitializeAsync(TestContext.Current.CancellationToken); Assert.False(session.RestorePending); Assert.Null(store.Value); Assert.Equal(0, store.BrowserOpens);
    }
}
