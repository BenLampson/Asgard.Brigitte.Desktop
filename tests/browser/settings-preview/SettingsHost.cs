using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;

namespace SettingsPreview;

/// <summary>仅使用虚构身份和临时 SQLite，拒绝外部网络与凭据访问。</summary>
public sealed class SettingsHost(NotificationPreferenceStore preferences) : IClientHost, ICredentialStore, ILoginBrowser, IHttpClientFactory, IWorkspaceSync
{
    public string ThemePreference { get; private set; } = "dark";
    public string Theme => ThemePreference == "system" ? "light" : ThemePreference;
    public string LanguagePreference { get; private set; } = "zh-CN";
    public string CloseBehavior { get; private set; } = "ask";
    public string AppVersion => "1.0.12";
    public string UpdateMessage => ClientText.T("尚未检查更新");
    public bool SupportsStoreUpdates => true;
    public bool SyncReady => true;
    public bool CanWrite => true;
    public string WorkerId => "preview";
    public string? RequestedProjectId => null;
    public event Action? Changed;
    public event Action? StatusChanged { add { } remove { } }
    public void SetTheme(string theme) { ThemePreference = theme; PublishChange(); }
    public void SetLanguage(string language) { LanguagePreference = language; ClientText.SetLanguage(language); PublishChange(); }
    public void SetCloseBehavior(string behavior) { CloseBehavior = behavior; PublishChange(); }
    public NotificationRule GetNotificationRule(string? projectId = null) => preferences.Get("preview-user", projectId);
    public bool HasNotificationOverride(string projectId) => preferences.HasOverride("preview-user", projectId);
    public void SetNotificationRule(string? projectId, NotificationRule? rule) { preferences.Set("preview-user", projectId, rule); PublishChange(); }
    public void PublishChange() => Changed?.Invoke();
    public Task OpenAsync(DesktopView view) => Task.CompletedTask;
    public void Close(DesktopView view) { }
    public Task CopyAsync(string text) => Task.CompletedTask;
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
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
