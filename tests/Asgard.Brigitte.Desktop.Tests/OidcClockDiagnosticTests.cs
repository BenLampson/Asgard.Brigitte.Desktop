using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Asgard.Heimdall.Client;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>使用真实 SDK 与本地签名的令牌验证时钟诊断和安全降级。</summary>
public sealed class OidcClockDiagnosticTests
{
    /// <summary>登录时分别显示电脑快、慢的方向与大致时差，始终拒绝无效令牌。</summary>
    [Theory]
    [InlineData(120, "慢", "2 分钟", "behind")]
    [InlineData(-1800, "快", "30 分钟", "ahead")]
    public async Task Login_reports_measured_clock_difference_in_both_languages(int serverAhead, string direction, string amount, string englishDirection)
    {
        var clock = new SessionTestClock();
        using var server = new SignedOidcTransport(clock) { ServerAheadSeconds = serverAhead, ReportServerDate = true };
        var store = LoginStore(server);
        using var session = server.Session(store);

        var error = await Assert.ThrowsAsync<ClientClockSkewException>(() => session.LoginAsync(TestContext.Current.CancellationToken));

        Assert.False(session.IsAuthenticated);
        Assert.Null(store.Value);
        Assert.Equal(new[] { false }, store.LoginResults);
        using (ClientText.UseLanguage("zh-CN"))
        {
            var message = AppView.SafeError(error);
            Assert.Contains("本机时间比登录服务" + direction, message);
            Assert.Contains(amount, message);
            Assert.Contains("自动设置日期、时间和时区", message);
            Assert.Contains("重新登录", message);
            Assert.DoesNotContain("test-code", message);
        }
        using (ClientText.UseLanguage("en"))
        {
            var message = AppView.SafeError(error);
            Assert.Contains(englishDirection, message);
            Assert.Contains("synchronize the clock", message);
        }
    }

    /// <summary>恢复凭据时的时间失败保留已存会话并提供相同诊断。</summary>
    [Fact]
    public async Task Restore_reports_clock_difference_without_clearing_saved_credentials()
    {
        var clock = new SessionTestClock();
        using var server = new SignedOidcTransport(clock) { ServerAheadSeconds = 120, ReportServerDate = true };
        var store = new MemoryCredentialStore { Value = server.Saved() };
        var saved = store.Value;
        using var session = server.Session(store);

        var error = await Assert.ThrowsAsync<ClientClockSkewException>(() => session.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.True(error.Difference < TimeSpan.Zero);
        Assert.True(session.RestorePending);
        Assert.False(session.IsAuthenticated);
        Assert.Equal(saved, store.Value);
        Assert.Equal(0, store.Clears);
    }

    /// <summary>运行期间续期失败也提供诊断，不把旧会话误当成已验证的新会话。</summary>
    [Fact]
    public async Task Renewal_reports_clock_difference_without_replacing_existing_session()
    {
        var clock = new SessionTestClock();
        using var server = new SignedOidcTransport(clock) { ReportServerDate = true };
        var store = LoginStore(server);
        using var session = server.Session(store);
        await session.LoginAsync(TestContext.Current.CancellationToken);
        var saved = store.Value;
        server.ServerAheadSeconds = 120;
        clock.Advance(TimeSpan.FromMinutes(14.5));

        var error = await Assert.ThrowsAsync<ClientClockSkewException>(() => session.GetLeaseAsync(TestContext.Current.CancellationToken));

        Assert.True(error.Difference < TimeSpan.Zero);
        Assert.True(session.IsAuthenticated);
        Assert.Equal(saved, store.Value);
    }

    /// <summary>探测不可用、无时间或响应来自缓存时，不编造具体时差。</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task Login_without_a_fresh_server_time_keeps_the_original_error(bool includeDate, bool failProbe, bool cached)
    {
        var clock = new SessionTestClock();
        using var server = new SignedOidcTransport(clock) { ServerAheadSeconds = 120, ReportServerDate = includeDate, FailClockProbe = failProbe, CachedClockProbe = cached };
        var store = LoginStore(server);
        using var session = server.Session(store);

        var error = await Assert.ThrowsAsync<HeimdallClientException>(() => session.LoginAsync(TestContext.Current.CancellationToken));

        Assert.Equal("invalid_jwt", error.Category);
        using var language = ClientText.UseLanguage("zh-CN");
        var message = AppView.SafeError(error);
        Assert.Contains("请检查电脑的日期、时间和时区", message);
        Assert.DoesNotContain("本机时间比登录服务", message);
        Assert.DoesNotContain("test-code", message);
    }

    /// <summary>确实过期或签名错误的令牌不能被当作已确认的时钟偏差。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invalid_token_without_clock_difference_never_claims_a_measured_skew(bool expired)
    {
        var clock = new SessionTestClock();
        using var server = new SignedOidcTransport(clock) { ReportServerDate = true, TokenIssuedOffsetSeconds = expired ? -1800 : 0, InvalidSignature = !expired };
        var store = LoginStore(server);
        using var session = server.Session(store);

        var error = await Assert.ThrowsAsync<HeimdallClientException>(() => session.LoginAsync(TestContext.Current.CancellationToken));

        Assert.Equal("invalid_jwt", error.Category);
        Assert.False(session.IsAuthenticated);
        using var language = ClientText.UseLanguage("zh-CN");
        Assert.DoesNotContain("本机时间比登录服务", AppView.SafeError(error));
    }

    /// <summary>探测过程中系统时间变化，或接近容差边界时，无法可靠给出时差。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Uncertain_measurement_does_not_claim_a_specific_clock_difference(bool clockChanged)
    {
        var clock = new SessionTestClock();
        using var server = new SignedOidcTransport(clock) { ServerAheadSeconds = clockChanged ? 120 : 31, ReportServerDate = true };
        if (clockChanged) server.OnClockProbe = () => clock.Advance(TimeSpan.FromMinutes(1));
        using var session = server.Session(LoginStore(server));

        var error = await Assert.ThrowsAsync<HeimdallClientException>(() => session.LoginAsync(TestContext.Current.CancellationToken));

        Assert.Equal("invalid_jwt", error.Category);
    }

    /// <summary>构造一次性的浏览器回调，nonce 和 state 由真实 SDK 生成。</summary>
    private static MemoryCredentialStore LoginStore(SignedOidcTransport server)
    {
        var store = new MemoryCredentialStore();
        store.LoginCallback = url =>
        {
            var query = new Uri(url).Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));
            server.LoginNonce = query["nonce"];
            return [new("code", "test-code"), new("state", query["state"])];
        };
        return store;
    }
}
