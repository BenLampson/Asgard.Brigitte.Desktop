using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop;
/// <summary>所有页面共享会话，独立窗口共享刷新事件而不改变主看板上下文。</summary>
public sealed partial class DesktopHost : IClientHost
{
    private readonly IServiceProvider _services;
    private readonly SessionService _session;
    private readonly WorkspaceStore _workspace;
    private readonly Dictionary<DesktopView, Window> _windows = [];
    private IPlatformTray? _tray;
    private Window? _main, _popup;
    private double _popupAnchorX, _popupAnchorY;
    private bool _quitting;
    private int _realtimePending;
    private readonly DesktopRealtime _realtime;
    private readonly StandaloneWorkspace _standalone;
    private readonly ThemePreferenceStore _theme;
    private readonly ClosePreferenceStore _closePreference;
    private readonly LanguagePreferenceStore _language;
    private readonly NotificationPreferenceStore _notificationPreferences;
    private readonly ClientOptions _options;
    /// <summary>同一连接状态供所有窗口显示。</summary>
    public string RealtimeStatus => _session.IsLocal ? ClientText.T("本机使用") : _realtime.Status;
    public bool SyncReady => _session.IsLocal ? _standalone.Ready : _realtime.Ready;
    public bool CanWrite => _session.IsLocal ? _standalone.Ready : _realtime.CanWrite;
    public bool SyncHasManifest => _realtime.HasManifest;
    public int SyncReceived => _realtime.Received;
    public int SyncTotal => _realtime.Total;
    public int PendingCommands => _session.IsLocal ? 0 : _realtime.PendingCommands;
    /// <summary>更新事件不携带敏感信息。</summary>
    public event Action? Changed;
    /// <summary>连接状态的轻量 UI 通知。</summary>
    public event Action? StatusChanged;
    /// <summary>本客户端持锁的稳定 Worker 标识，不伪造用户身份。</summary>
    public string WorkerId { get; }
    /// <summary>托盘选定项目传回原有主窗口。</summary>
    public string? RequestedProjectId { get; private set; }
    /// <summary>仅保存 UI 偏好。</summary>
    public string ThemePreference => _theme.Mode;
    /// <summary>供 CSS 使用的有效日夜模式。</summary>
    public string Theme => ThemePreference == "system" ? Application.Current?.RequestedTheme == AppTheme.Dark ? "dark" : "light" : ThemePreference;
    /// <summary>原生服务注入，不由组件持有平台句柄。</summary>
    public DesktopHost(IServiceProvider services, SessionService session, WorkspaceStore workspace, DesktopRealtime realtime, ThemePreferenceStore theme, ClosePreferenceStore closePreference, LanguagePreferenceStore language, StandaloneWorkspace standalone, NotificationPreferenceStore notificationPreferences, IOptions<ClientOptions> options)
    {
        _services = services; _session = session; _workspace = workspace; _realtime = realtime; _theme = theme;
        _closePreference = closePreference;
        _language = language;
        _notificationPreferences = notificationPreferences;
        _options = options.Value;
        _standalone = standalone;
        standalone.Changed += RealtimeChanged;
        ClientText.SetLanguage(language.Language);
        realtime.Changed += RealtimeChanged;
        realtime.Notification += NotifyCommitted;
        realtime.StatusChanged += () => MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke());
        workspace.Changed += () => MainThread.BeginInvokeOnMainThread(() => _tray?.Refresh());
        WorkerId = Preferences.Default.Get("brigitte.worker", "");
        if (WorkerId == "") { WorkerId = "brigitte-desktop-" + Guid.NewGuid().ToString("N"); Preferences.Default.Set("brigitte.worker", WorkerId); }
        session.Changed += SessionChanged;
    }
    /// <summary>主窗与登录门禁共用一扇窗口。</summary>
    public Window CreateMainWindow()
    {
        _main = Create(new(DesktopViewKind.Main));
        _main.Created += (_, _) => { _tray = CreateTray(); _tray.Refresh(); StartUpdateChecks(); };
        return _main;
    }
    private Window Create(DesktopView view, Window? owner = null)
    {
        var web = new BlazorWebView { HostPage = "wwwroot/index.html" };
#if WINDOWS
        // MSIX 安装目录只读，所有窗口共用用户目录中的 WebView2 数据，避免首次打开白屏。
        web.BlazorWebViewInitializing += (_, args) =>
        {
            var directory = Path.Combine(FileSystem.AppDataDirectory, "WebView2");
            Directory.CreateDirectory(directory);
            args.UserDataFolder = directory;
        };
#endif
        web.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(AppView), Parameters = new Dictionary<string, object?> { { nameof(AppView.View), view } } });
        web.UrlLoading += (_, e) =>
        {
            if (e.Url.Host == "0.0.0.1") return;
            e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
            if (e.Url.Scheme is "https" or "http") _ = OpenExternalAsync(e.Url.AbsoluteUri);
        };
        var window = new Window(new ContentPage { Content = web, BackgroundColor = Theme == "dark" ? Color.FromArgb("#202428") : Colors.White }) { Title = Title(view.Kind) + (view.Id is null ? "" : " · " + BugDisplay.ShortId(view.Id)), Width = 1120, Height = 800, MinimumWidth = 760, MinimumHeight = 520 };
        if (view.Kind == DesktopViewKind.Main) { window.Width = 1440; window.Height = 900; window.MinimumWidth = 1120; window.MinimumHeight = 700; }
        if (view.Kind == DesktopViewKind.Status) { window.Width = 560; window.Height = 480; window.MinimumWidth = 480; window.MinimumHeight = 400; }
        if (view.Kind is DesktopViewKind.Settings or DesktopViewKind.About or DesktopViewKind.Privacy) { window.Width = 760; window.Height = 680; window.MinimumWidth = 620; window.MinimumHeight = 520; }
        if (view.Kind == DesktopViewKind.About) { window.MinimumWidth = 680; window.MinimumHeight = 620; }
        // 项目表单按内容设置初始尺寸；管理员额外的负责人和成员选项需要更多高度。
        if (view.Kind == DesktopViewKind.ProjectCreate) { window.MinimumWidth = 520; window.MinimumHeight = 480; window.Width = 640; window.Height = _workspace.Access.IsTenantAdmin ? 720 : 560; }
        if (view.Kind == DesktopViewKind.ProjectEdit) { window.MinimumWidth = 560; window.MinimumHeight = 520; window.Width = 720; window.Height = 720; }
        if (view.Kind is DesktopViewKind.ProjectInvitations or DesktopViewKind.JoinProject) { window.Width = 720; window.Height = 760; window.MinimumWidth = 600; window.MinimumHeight = 520; }
        if (view.Kind == DesktopViewKind.Tray)
        {
            // 托盘内容已有品牌栏；隐藏 MAUI 绘制的标题栏，避免留下重复标题和主题色残片。
            window.TitleBar = new TitleBar { IsVisible = false };
            ApplyTraySize(window);
            window.Deactivated += (_, _) => HidePopup();
        }
        _windows[view] = window;
        window.Activated += (_, _) => { _activeView = view; RefreshSystemLanguage(); };
        window.Deactivated += (_, _) => { if (_activeView == view) _activeView = null; };
        window.Destroying += (_, _) => { _windows.Remove(view); if (_activeView == view) _activeView = null; if (view.Kind == DesktopViewKind.Tray) _popup = null; };
        window.HandlerChanged += (_, _) => AttachPlatformWindow(window, view.Kind, owner);
        return window;
    }
    /// <summary>重复打开同一 BUG 时激活现有独立窗口。</summary>
    public Task OpenAsync(DesktopView view) => OpenAsync(view, null);
    /// <summary>记录发起窗口，异步操作期间切换焦点也不会改变新窗口的居中基准。</summary>
    public Task OpenAsync(DesktopView view, DesktopView? owner) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (view.Kind == DesktopViewKind.Main)
        {
            if (view.ProjectId is not null) RequestedProjectId = view.ProjectId;
            if (_main is not null) Show(_main, true);
            Changed?.Invoke(); HidePopup(); return;
        }
        if (!_session.HasWorkspace && view.Kind is not (DesktopViewKind.Tray or DesktopViewKind.Settings or DesktopViewKind.About or DesktopViewKind.Privacy)) return;
        if (_session.IsLocal && view.Kind is DesktopViewKind.Keys or DesktopViewKind.KeyCreate or DesktopViewKind.KeyResult or DesktopViewKind.Users or DesktopViewKind.ProjectInvitations or DesktopViewKind.JoinProject) return;
        if (_windows.TryGetValue(view, out var existing)) { Show(existing, true); return; }
        Application.Current!.OpenWindow(Create(view, ResolveOwner(owner)));
    });
    /// <summary>优先使用调用方；原生入口使用当前活动窗口，托盘浮层不作为业务窗口的 owner。</summary>
    private Window? ResolveOwner(DesktopView? owner)
    {
        if (owner is { Kind: not DesktopViewKind.Tray } && _windows.TryGetValue(owner, out var source)) return source;
        if (_activeView is { Kind: not DesktopViewKind.Tray } && _windows.TryGetValue(_activeView, out var active)) return active;
        return _main;
    }
    /// <summary>独立窗口关闭不会替换或关闭主看板。</summary>
    public void Close(DesktopView view) { if (_windows.TryGetValue(view, out var window)) Application.Current!.CloseWindow(window); }
    /// <summary>一次数据变更刷新已打开的只读窗口。</summary>
    public void PublishChange() { Changed?.Invoke(); _tray?.Refresh(); }
    /// <summary>合并连续通知，刷新只读窗口，编辑窗口不订阅此事件。</summary>
    private void RealtimeChanged()
    {
        if (Interlocked.Exchange(ref _realtimePending, 1) != 0) return;
        MainThread.BeginInvokeOnMainThread(async () => { await Task.Delay(250); Interlocked.Exchange(ref _realtimePending, 0); PublishChange(); });
    }
    /// <summary>主题随全部窗口和托盘同步。</summary>
    public void SetTheme(string theme) { if (theme is not ("light" or "dark" or "system")) return; _theme.Set(theme); Application.Current!.UserAppTheme = theme switch { "light" => AppTheme.Light, "dark" => AppTheme.Dark, _ => AppTheme.Unspecified }; NotifyThemeChanged(); }
    /// <summary>跟随系统外观切换。</summary>
    public void NotifyThemeChanged() { foreach (var window in _windows.Values) if (window.Page is not null) window.Page.BackgroundColor = Theme == "dark" ? Color.FromArgb("#202428") : Colors.White; ApplyPlatformTheme(); Changed?.Invoke(); _tray?.Refresh(); }
    /// <summary>使用原生剪贴板，不通过网页权限。</summary>
    public Task CopyAsync(string value) => Clipboard.Default.SetTextAsync(value);
    private Page ActivePage => Application.Current!.Windows.LastOrDefault(w => w != _popup)?.Page ?? _main?.Page ?? throw new InvalidOperationException(ClientText.T("主窗口尚未创建。"));
    /// <summary>复用平台确认对话框。</summary>
    public Task<bool> ConfirmAsync(string title, string message) => MainThread.InvokeOnMainThreadAsync(() => ActivePage.DisplayAlertAsync(title, message, ClientText.T("确认"), ClientText.T("取消")));
    /// <summary>读取用户显式选择的多文件，限制单附件 50 MB。</summary>
    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync()
    {
        var selected = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = ClientText.T("选择 BUG 附件") });
        var result = new List<PickedFile>();
        foreach (var file in selected ?? []) { await using var stream = await file.OpenReadAsync(); if (stream.Length > 50 * 1024 * 1024) throw new InvalidOperationException(ClientText.T("单附件不能超过 50 MB。")); using var memory = new MemoryStream(); await stream.CopyToAsync(memory); result.Add(new(file.FileName, file.ContentType ?? "application/octet-stream", memory.ToArray())); }
        return result;
    }
    /// <summary>保存通过原生平台的另存为对话框完成。</summary>
    public Task SaveAsync(string fileName, byte[] bytes) => SavePlatformAsync(Path.GetFileName(fileName), bytes);
    /// <summary>只允许外部系统浏览器打开 HTTP(S)。</summary>
    public async Task OpenExternalAsync(string url) { if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") await Launcher.Default.OpenAsync(uri); }
    private void SessionChanged() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (!_session.HasWorkspace)
        {
            ClearNotifications();
            _workspace.Clear(); ImagePreviewStore.Clear(); KeyResultStore.Clear(); RequestedProjectId = null;
            foreach (var pair in _windows.Where(p => p.Key.Kind is not (DesktopViewKind.Main or DesktopViewKind.Settings or DesktopViewKind.About or DesktopViewKind.Privacy)).ToArray()) Application.Current!.CloseWindow(pair.Value);
            if (_main is not null) Show(_main, true);
        }
        if (_session.HasWorkspace && _popup is not null) ApplyTraySize(_popup);
        Changed?.Invoke(); _tray?.Refresh();
    });
    /// <summary>用户点击托盘后展示现有项目及数量。</summary>
    internal async void ShowPopup(double x, double y)
    {
        try
        {
            _popupAnchorX = x; _popupAnchorY = y;
            if (_popup is null) { var view = new DesktopView(DesktopViewKind.Tray); _popup = Create(view); Application.Current!.OpenWindow(_popup); }
            ApplyTraySize(_popup); Show(_popup, true); PositionPopup(_popup, x, y);
            if (_session.HasWorkspace) { await _workspace.LoadAsync(); Changed?.Invoke(); }
        }
        catch { /* 同步服务自动重试，托盘消息回调不抛出。 */ }
    }
    /// <summary>未登录使用设计稿的紧凑尺寸；复用窗口时恢复已登录的项目布局。</summary>
    private void ApplyTraySize(Window window)
    {
        window.MinimumWidth = 320;
        window.MinimumHeight = 220;
        window.Width = _session.HasWorkspace ? 400 : 360;
        var noticesHeight = RecentChanges.Count == 0 ? 0 : 258;
        window.Height = _session.HasWorkspace ? Math.Min(540, 172 + Math.Max(2, Projects.Count) * 44 + noticesHeight) : 220;
    }
    internal void HidePopup() { if (_popup is not null) Show(_popup, false); }
    /// <summary>仅供平台适配检查浮层原生边界。</summary>
    internal Window? PopupWindow => _popup;
    internal IReadOnlyList<Asgard.Brigitte.Desktop.Core.Models.Project> Projects => _session.HasWorkspace ? _workspace.Projects : [];
    internal long ProjectCount(string id) => _workspace.Stats(id)?.Unresolved ?? 0;
    internal bool IsAuthenticated => _session.HasWorkspace;
    internal void RefreshTrayData() => MainThread.BeginInvokeOnMainThread(async () => { try { if (_session.HasWorkspace) await _workspace.LoadAsync(); PublishChange(); } catch {/* 下次显式刷新可重试。 */} });
    private void Show(Window window, bool visible) { if (_tray is not null) _tray.Show(window, visible); else if (visible) Application.Current!.ActivateWindow(window); }
    /// <summary>显式退出才终止程序并移除系统图标。</summary>
    public void Quit() { _quitting = true; _tray?.Dispose(); Application.Current!.Quit(); }
    private static string Title(DesktopViewKind kind) => "Asgard Brigitte · " + (kind switch { DesktopViewKind.Main => ClientText.T("BUG 管理器"), DesktopViewKind.Detail => ClientText.T("BUG 详情"), DesktopViewKind.Create => ClientText.T("提交 BUG"), DesktopViewKind.Edit => ClientText.T("编辑 BUG"), DesktopViewKind.Tray => ClientText.T("项目"), DesktopViewKind.Settings => ClientText.T("设置"), DesktopViewKind.About => ClientText.T("关于"), DesktopViewKind.Privacy => ClientText.T("隐私与数据"), DesktopViewKind.ProjectInvitations => ClientText.T("邀请项目成员"), DesktopViewKind.JoinProject => ClientText.T("加入项目"), _ => ClientText.T("BUG 管理") });
    private partial IPlatformTray CreateTray();
    private partial void AttachPlatformWindow(Window window, DesktopViewKind kind, Window? owner);
    private partial Task SavePlatformAsync(string fileName, byte[] bytes);
    private partial void ApplyPlatformTheme();
    private partial void PositionPopup(Window window, double x, double y);
}
