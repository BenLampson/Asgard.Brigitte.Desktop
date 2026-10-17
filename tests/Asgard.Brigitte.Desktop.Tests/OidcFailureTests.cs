using System.Net;
using Asgard.Heimdall.Client;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>确认实际 SDK 的稳定错误分类，用于精确失效处理。</summary>
public sealed class OidcFailureTests
{
    [Fact]
    public async Task Invalid_refresh_grant_is_classified_without_token_or_server_body()
    {
        using var transport = new TestTransport(request => Task.FromResult(request.Method == HttpMethod.Get
            ? TestTransport.Json("{\"issuer\":\"https://issuer.test/tenant\",\"authorization_endpoint\":\"https://issuer.test/tenant/authorize\",\"token_endpoint\":\"https://issuer.test/tenant/token\",\"jwks_uri\":\"https://issuer.test/tenant/jwks\"}")
            : TestTransport.Json("{\"error\":\"invalid_grant\",\"error_description\":\"do-not-expose\"}", HttpStatusCode.BadRequest)));
        using var http = transport.CreateClient("oidc");
        using var client = new HeimdallOAuthClient(new HeimdallOAuthOptions { Authority = "https://issuer.test/tenant", ClientId = "desktop", Audience = "api", TenantId = "tenant", ApplicationId = "app", Scope = "openid profile offline_access api", RedirectUri = "http://127.0.0.1:51843/callback", PostLogoutRedirectUri = "http://127.0.0.1:51843/callback", AllowLocalhostHttp = true, AuthenticationMethod = "none", AccessTokenFormat = "jwt" }, httpClient: http);
        var ex = await Assert.ThrowsAsync<HeimdallClientException>(() => client.RefreshTokenAsync("fake-refresh", "subject", "session", TestContext.Current.CancellationToken));
        Assert.Equal("invalid_grant", ex.Category); Assert.DoesNotContain("do-not-expose", ex.Message); Assert.DoesNotContain("fake-refresh", ex.Message);
    }
}
