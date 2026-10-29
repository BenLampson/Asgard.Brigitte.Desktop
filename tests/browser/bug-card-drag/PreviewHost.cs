using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;

namespace BugCardDragPreview;

/// <summary>只服务虚构本机数据的浏览器验证宿主，拒绝凭据和外部网络。</summary>
public sealed class PreviewHost : IClientHost, ICredentialStore, ILoginBrowser, IHttpClientFactory, IWorkspaceSync
{
    public string ThemePreference => Theme;
    public string Theme { get; private set; } = "dark";
    public bool CanWrite { get; set; } = true;
    public string WorkerId => "preview-worker";
    public string? RequestedProjectId => null;
    public bool ConfirmResult { get; set; }
    public int Confirmations { get; private set; }
    public int Opened { get; private set; }
    public int Copies { get; private set; }
    public event Action? Changed;
    public event Action? StatusChanged { add { } remove { } }
    public void PublishChange() => Changed?.Invoke();
    public void SetTheme(string theme) { Theme = theme; PublishChange(); }
    public Task OpenAsync(DesktopView view) { Opened++; PublishChange(); return Task.CompletedTask; }
    public void Close(DesktopView view) { }
    public Task CopyAsync(string text) { Copies++; PublishChange(); return Task.CompletedTask; }
    public Task<bool> ConfirmAsync(string title, string message) { Confirmations++; return Task.FromResult(ConfirmResult); }
    public Task<IReadOnlyList<PickedFile>> PickFilesAsync() => Task.FromResult<IReadOnlyList<PickedFile>>([]);
    public Task<bool> SaveAsync(string name, byte[] bytes) => Task.FromResult(false);
    public Task OpenExternalAsync(string url) => throw new InvalidOperationException("Preview cannot open external links");
    public void Quit() { }
    public Task<string?> ReadAsync() => throw new InvalidOperationException("Preview cannot read credentials");
    public Task WriteAsync(string value) => throw new InvalidOperationException("Preview cannot write credentials");
    public Task ClearAsync() => Task.CompletedTask;
    public Task<IReadOnlyList<KeyValuePair<string, string>>> AuthorizeAsync(string redirect, string url, CancellationToken token) => throw new InvalidOperationException("Preview cannot sign in");
    public Task CompleteAsync(bool success) => Task.CompletedTask;
    public HttpClient CreateClient(string name) => throw new InvalidOperationException("Preview cannot access the network");
    public Task ConfirmAsync(CancellationToken token = default) => Task.CompletedTask;
}
