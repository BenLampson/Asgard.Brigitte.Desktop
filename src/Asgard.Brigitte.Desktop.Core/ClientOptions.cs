namespace Asgard.Brigitte.Desktop.Core;
/// <summary>资源服务器、授权服务器和管理站点独立配置。</summary>
public sealed class ClientOptions
{
    /// <summary>现有 BUG 服务入口。</summary>
    public string ApiBaseUrl { get; set; } = "https://brigitte.mudou.tech/";
    /// <summary>精确 OIDC issuer。</summary>
    public string Authority { get; set; } = "https://idp.mudou.tech/a962107b-f8bf-4c2f-aef0-3a503b0607b1";
    /// <summary>Heimdall 已登记的 Brigitte Desktop 原生 public client。</summary>
    public string ClientId { get; set; } = "c3c29924-3bdd-4ed1-8b5c-b7d32e36ba1d";
    /// <summary>预期租户。</summary>
    public string TenantId { get; set; } = "a962107b-f8bf-4c2f-aef0-3a503b0607b1";
    /// <summary>Heimdall 应用标识，必须由登记值提供。</summary>
    public string ApplicationId { get; set; } = "871fbf1bdce7d62d36c2cc2112e9a11b";
    /// <summary>现有 API 受众，与 native client ID 独立。</summary>
    public string Audience { get; set; } = "e6b956c2-d201-4493-b13f-e44efd6c26ec";
    /// <summary>现有资源权限。</summary>
    public string Scope { get; set; } = "openid profile offline_access brigitte.api";
    /// <summary>需登记的本机回调，仅绑定 loopback。</summary>
    public string RedirectUri { get; set; } = "http://127.0.0.1:51843/callback";
    /// <summary>独立的账号管理前端地址。</summary>
    public string HeimdallWebBaseUrl { get; set; } = "https://heimdall.mudou.tech/";
    /// <summary>端点检查不猜测客户端登记值。</summary>
    public bool IsValid()
    {
        return Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var api) && api.Scheme == "https"
            && Uri.TryCreate(Authority, UriKind.Absolute, out var issuer) && issuer.Scheme == "https"
            && Uri.TryCreate(HeimdallWebBaseUrl, UriKind.Absolute, out var web) && web.Scheme == "https"
            && Uri.TryCreate(RedirectUri, UriKind.Absolute, out var callback)
            && callback.Scheme == "http" && callback.Host == "127.0.0.1" && callback.AbsolutePath == "/callback"
            && callback.Port > 1024;
    }
    /// <summary>官方 SDK 负责 PKCE、nonce、签名和身份边界校验。</summary>
    public HeimdallOAuthOptions OAuthOptions() => new()
    {
        Authority = Authority,
        ClientId = ClientId,
        TenantId = TenantId,
        ApplicationId = ApplicationId,
        Audience = Audience,
        Scope = Scope,
        RedirectUri = RedirectUri,
        PostLogoutRedirectUri = RedirectUri,
        AllowLocalhostHttp = true,
        AuthenticationMethod = "none",
        AccessTokenFormat = "jwt",
        // 官方 SDK 对 iat、nbf、exp 统一使用 30 秒容差，不改变续期时间和机器时钟。
        ClockSkewSeconds = 30
    };
}
