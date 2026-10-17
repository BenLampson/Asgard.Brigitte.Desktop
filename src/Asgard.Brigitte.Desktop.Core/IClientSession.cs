namespace Asgard.Brigitte.Desktop.Core;
/// <summary>资源请求只能消费带版本的用户会话。</summary>
public interface IClientSession
{
    /// <summary>当前会话版本。</summary>
    long Version { get; }
    /// <summary>身份代际；同一会话的正常令牌轮换不使成功响应过期。</summary>
    long IdentityVersion => Version;
    /// <summary>获取新鲜凭据，不自动开启 SSO。</summary>
    Task<TokenLease> GetLeaseAsync(CancellationToken cancellationToken = default);
    /// <summary>仅失效捕获的会话。</summary>
    Task InvalidateAsync(long version);
}
