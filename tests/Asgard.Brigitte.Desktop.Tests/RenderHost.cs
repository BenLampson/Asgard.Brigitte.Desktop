using Asgard.Brigitte.Desktop.UI;
using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>组件渲染测试的原生能力边界，不操作用户剪贴板或文件。</summary>
internal sealed class RenderHost(string theme) : IClientHost
{
    public string ThemePreference => theme;
    public string Theme => theme;
    public string LanguagePreference { get; private set; } = "system";
    public string Language => LanguagePreference == "system" ? ClientText.Language : LanguagePreference;
    public void SetLanguage(string language) { LanguagePreference = language; PublishStatus(); }
    /// <summary>提醒交互测试可接入真实本地存储，普通渲染测试沿用默认规则。</summary>
    public NotificationPreferenceStore? NotificationPreferences { get; init; }
    public NotificationRule GetNotificationRule(string? projectId = null) => NotificationPreferences?.Get("render-test", projectId) ?? new();
    public bool HasNotificationOverride(string projectId) => NotificationPreferences?.HasOverride("render-test", projectId) ?? false;
    public void SetNotificationRule(string? projectId, NotificationRule? rule) { NotificationPreferences?.Set("render-test", projectId, rule); PublishStatus(); }
    public bool SyncReady { get; init; } = true;
    public bool CanWrite => true;
    public bool SyncHasManifest { get; set; } = true;
    public int SyncReceived => 1260;
    public int SyncTotal => 1840;
    private string _realtimeStatus = "正在同步数据";
    public string RealtimeStatus { get => ClientText.T(_realtimeStatus); set => _realtimeStatus = value; }
    public string NotificationText => "已同步 3 项更新";
    public bool ShowNotification(DesktopView view) => view.Kind == DesktopViewKind.Main;
    public string WorkerId => "desktop-test-worker";
    public string? RequestedProjectId => null;
    public event Action? Changed { add { } remove { } }
    public event Action? StatusChanged;
    public void PublishStatus() => StatusChanged?.Invoke();
    public void SetTheme(string theme) { }
    public Task OpenAsync(DesktopView view) => Task.CompletedTask;
    public void Close(DesktopView view) { }
    public void PublishChange() { }
    public Task CopyAsync(string value) => Task.CompletedTask;
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
    public Task<IReadOnlyList<PickedFile>> PickFilesAsync() => Task.FromResult<IReadOnlyList<PickedFile>>([]);
    public Task SaveAsync(string name, byte[] bytes) => Task.CompletedTask;
    public Task OpenExternalAsync(string url) => Task.CompletedTask;
    public void Quit() { }
}
