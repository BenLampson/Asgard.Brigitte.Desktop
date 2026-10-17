using System.Text.Json;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Heimdall.Client;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>使用应用实际配置和官方 SDK 验证时差，不校准机器时钟或绕过身份边界。</summary>
public sealed class OidcClockSkewTests
{
    /// <summary>覆盖真实授权码兑换、ID Token/AT 验签、资料读取和轮换保存的整条链路。</summary>
    [Fact]
    public async Task Seven_second_difference_allows_login_restart_and_idle_refresh()
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock) { ServerAheadSeconds = 7 }; var store = new MemoryCredentialStore();
        store.LoginCallback = url =>
        {
            var query = new Uri(url).Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));
            server.LoginNonce = query["nonce"];
            Assert.Empty(store.LoginResults);
            return [new("code", "test-code"), new("state", query["state"])];
        };
        using (var login = server.Session(store))
        {
            await login.LoginAsync(TestContext.Current.CancellationToken); Assert.True(login.IsAuthenticated); Assert.Equal("登录的账号", login.DisplayName); Assert.Equal(new[] { true }, store.LoginResults); Assert.NotNull(store.Value);
        }
        using var restarted = server.Session(store); await restarted.InitializeAsync(TestContext.Current.CancellationToken); Assert.True(restarted.IsAuthenticated);
        clock.Advance(TimeSpan.FromMinutes(14.5)); await restarted.MaintainAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, server.Refreshes); Assert.Equal("rotated-2", JsonSerializer.Deserialize<StoredSession>(store.Value!)!.Tokens.RefreshToken); Assert.Equal(1, store.BrowserOpens);
    }
    /// <summary>SDK 仍遵守零容差和客户配置边界，不接受无限时间偏差。</summary>
    [Theory]
    [InlineData(0, 7)]
    [InlineData(30, 31)]
    public async Task Official_validator_still_rejects_future_tokens_outside_configured_tolerance(int skew, int ahead)
    {
        var clock = new SessionTestClock(); using var server = new SignedOidcTransport(clock) { ServerAheadSeconds = ahead };
        var configured = server.Options.OAuthOptions();
        var options = new HeimdallOAuthOptions { Authority = configured.Authority, ClientId = configured.ClientId, TenantId = configured.TenantId, ApplicationId = configured.ApplicationId, Audience = configured.Audience, Scope = configured.Scope, RedirectUri = configured.RedirectUri, PostLogoutRedirectUri = configured.PostLogoutRedirectUri, AllowLocalhostHttp = true, AuthenticationMethod = "none", AccessTokenFormat = "jwt", ClockSkewSeconds = skew };
        using var http = server.CreateClient("oidc"); using var sdk = new HeimdallOAuthClient(options, httpClient: http, timeProvider: clock);
        var error = await Assert.ThrowsAsync<HeimdallClientException>(() => sdk.ValidateJwtAsync(server.Token("subject", "session"), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("invalid_jwt", error.Category);
    }
}
