using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>跨 SessionService 实例保留同一份凭据，模拟关闭后重新打开。</summary>
internal sealed class MemoryCredentialStore : ICredentialStore, ILoginBrowser
{
    internal string? Value;
    internal bool FailWrites;
    internal int Reads, Writes, Clears, BrowserOpens;
    internal Func<string, IReadOnlyList<KeyValuePair<string, string>>>? LoginCallback;
    internal readonly List<bool> LoginResults = [];
    public Task<string?> ReadAsync() { Reads++; return Task.FromResult(Value); }
    public Task WriteAsync(string value) { if (FailWrites) throw new IOException("test storage unavailable"); Value = value; Writes++; return Task.CompletedTask; }
    public Task ClearAsync() { Value = null; Clears++; return Task.CompletedTask; }
    public Task<IReadOnlyList<KeyValuePair<string, string>>> AuthorizeAsync(string redirect, string url, CancellationToken token) { BrowserOpens++; return LoginCallback is null ? throw new NotSupportedException("Restoration must not open SSO") : Task.FromResult(LoginCallback(url)); }
    public Task CompleteAsync(bool success) { LoginResults.Add(success); return Task.CompletedTask; }
}
