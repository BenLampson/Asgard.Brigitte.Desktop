using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Heimdall.Client;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>用本地 RSA/JWKS 驱动真正的 Heimdall SDK；不绕过验签、不访问生产。</summary>
internal sealed class SignedOidcTransport(SessionTestClock clock) : HttpMessageHandler, IHttpClientFactory
{
    private readonly RSA _rsa = RSA.Create(2048);
    internal int Refreshes, Requests, ServerAheadSeconds;
    internal bool Offline, InvalidGrant;
    internal string? LoginNonce;
    internal readonly List<string> PresentedRefreshTokens = [];
    internal readonly TaskCompletionSource Renewed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ClientOptions Options { get; } = new() { Authority = "https://issuer.test/tenant", ClientId = "desktop", TenantId = "tenant", ApplicationId = "app", Audience = "api", Scope = "openid profile offline_access api" };
    public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("https://issuer.test/") };
    internal SessionService Session(MemoryCredentialStore store) => new(Microsoft.Extensions.Options.Options.Create(Options), this, store, store, clock);
    internal string Saved(string subject = "subject", string sid = "session") => JsonSerializer.Serialize(new StoredSession { Tokens = new HeimdallTokenSet { AccessToken = Token(subject, sid), RefreshToken = "initial-refresh", ExpiresIn = 900 }, SubjectId = subject, SessionId = sid, DisplayName = "保存的账号", ExpiresAt = clock.GetUtcNow().AddMinutes(15) });
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        if (Offline) throw new HttpRequestException("test network unavailable");
        if (request.RequestUri!.AbsolutePath.EndsWith("openid-configuration")) return Json(new { issuer = Options.Authority, authorization_endpoint = Options.Authority + "/authorize", token_endpoint = Options.Authority + "/token", jwks_uri = Options.Authority + "/jwks", revocation_endpoint = Options.Authority + "/revoke", userinfo_endpoint = Options.Authority + "/userinfo" });
        if (request.RequestUri.AbsolutePath.EndsWith("/jwks")) { var key = _rsa.ExportParameters(false); return Json(new { keys = new[] { new { kty = "RSA", kid = "test-key", alg = "RS256", use = "sig", n = B64(key.Modulus!), e = B64(key.Exponent!) } } }); }
        if (request.RequestUri.AbsolutePath.EndsWith("/token"))
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (body.Contains("grant_type=authorization_code", StringComparison.Ordinal)) return Json(new { access_token = Token("subject", "session"), id_token = Token("subject", "session", true), token_type = "Bearer", expires_in = 900, refresh_token = "initial-refresh", scope = "openid profile offline_access api" });
            var rt = body.Split('&').Select(p => p.Split('=', 2)).First(p => p[0] == "refresh_token")[1]; PresentedRefreshTokens.Add(Uri.UnescapeDataString(rt));
            Refreshes++;
            if (InvalidGrant) return TestTransport.Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest);
            var response = Json(new { access_token = Token("subject", "session"), token_type = "Bearer", expires_in = 900, refresh_token = "rotated-" + Refreshes, scope = "openid profile offline_access api" });
            if (Refreshes > 1) Renewed.TrySetResult(); return response;
        }
        if (request.RequestUri.AbsolutePath.EndsWith("/revoke")) return Json(new { });
        if (request.RequestUri.AbsolutePath.EndsWith("/userinfo")) return Json(new { sub = "subject", name = "登录的账号" });
        throw new InvalidOperationException("Unexpected SDK endpoint");
    }
    /// <summary>真实签名并带 nbf，复现 IDP 比机器时间快几秒的登录失败。</summary>
    internal string Token(string subject, string sid, bool idToken = false)
    {
        var issued = clock.GetUtcNow().AddSeconds(ServerAheadSeconds);
        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = "test-key", typ = "JWT" }));
        var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new { iss = Options.Authority, aud = idToken ? Options.ClientId : Options.Audience, sub = subject, sid, nonce = idToken ? LoginNonce : null, tenant_id = "tenant", application_id = "app", client_id = "desktop", token_type = "UserLogin", scope = "openid profile offline_access api", iat = issued.ToUnixTimeSeconds(), nbf = issued.ToUnixTimeSeconds(), exp = issued.AddMinutes(15).ToUnixTimeSeconds() }));
        var input = header + "." + payload; return input + "." + B64(_rsa.SignData(Encoding.UTF8.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }
    private static string B64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static HttpResponseMessage Json(object value) => TestTransport.Json(JsonSerializer.Serialize(value));
    protected override void Dispose(bool disposing) { if (disposing) _rsa.Dispose(); base.Dispose(disposing); }
}
