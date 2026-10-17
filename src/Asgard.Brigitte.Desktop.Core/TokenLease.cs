namespace Asgard.Brigitte.Desktop.Core;
/// <summary>请求捕获的会话版本，拒绝迟到结果覆盖新会话。</summary>
public sealed record TokenLease(long Version, string AccessToken)
{
    /// <summary>捕获取得凭据时的身份，令牌轮换期间仍可接纳同一身份的成功响应。</summary>
    public long IdentityVersion { get; init; } = Version;
    /// <summary>禁止对象文本泄漏凭据。</summary>
    public override string ToString() => $"Session version {Version}";
}
