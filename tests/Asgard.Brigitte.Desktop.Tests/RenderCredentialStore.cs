using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>测试专用空安全存储。</summary>
internal sealed class RenderCredentialStore : ICredentialStore, ILoginBrowser
{
    public Task<string?> ReadAsync() => Task.FromResult<string?>(null);
    public Task WriteAsync(string value) => Task.CompletedTask;
    public Task ClearAsync() => Task.CompletedTask;
    public Task<IReadOnlyList<KeyValuePair<string, string>>> AuthorizeAsync(string callback, string url, CancellationToken token) => throw new NotSupportedException();
}
