using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>模拟会话代际，验证旧请求不能污染新身份。</summary>
internal sealed class TestSession : IClientSession
{
    public long Version { get; set; } = 1;
    internal bool Authenticated = true;
    internal int Invalidations;
    public Task<TokenLease> GetLeaseAsync(CancellationToken cancellationToken = default) => Authenticated ? Task.FromResult(new TokenLease(Version, "test-access-token")) : throw new UnauthorizedAccessException();
    public Task InvalidateAsync(long version) { if (Version == version) { Authenticated = false; Version++; Invalidations++; } return Task.CompletedTask; }
}
