namespace Asgard.Brigitte.Desktop.Core;
/// <summary>仅保存于系统安全存储，禁止写入日志。</summary>
public sealed class StoredSession
{
    /// <summary>官方 SDK 凭据。</summary>
    public HeimdallTokenSet Tokens { get; set; } = null!;
    /// <summary>已验证主体。</summary>
    public string SubjectId { get; set; } = "";
    /// <summary>已验证会话。</summary>
    public string SessionId { get; set; } = "";
    /// <summary>访问令牌有效期。</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>仅显示的资料名。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>避免默认对象表示包含令牌。</summary>
    public override string ToString() => "[受保护的 Brigitte 会话]";
}
