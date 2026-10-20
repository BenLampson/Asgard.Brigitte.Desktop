namespace Asgard.Brigitte.Desktop;

/// <summary>更新状态独立于业务连接，启动和每日检查不阻塞本机工作区。</summary>
public sealed partial class DesktopHost
{
    private IDispatcherTimer? _updateTimer;
    private DateTimeOffset _updateSnoozedUntil;
    private string _updateMessage = "尚未检查更新";
    public bool UpdateBusy { get; private set; }
    public bool UpdateAvailable { get; private set; }
    public bool ShowUpdateReminder => UpdateAvailable && DateTimeOffset.UtcNow >= _updateSnoozedUntil;
    public string UpdateMessage => ClientText.T(_updateMessage);
    /// <summary>只有商店签名的正式包才使用商店更新 API。</summary>
    public bool SupportsStoreUpdates
    {
        get
        {
#if WINDOWS
            try { return Windows.ApplicationModel.Package.Current.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.Store; }
            catch (InvalidOperationException) { return false; }
            catch (System.Runtime.InteropServices.COMException) { return false; }
#else
            return false;
#endif
        }
    }
    private void StartUpdateChecks()
    {
        if (!SupportsStoreUpdates || _main is null || _updateTimer is not null) return;
        _updateTimer = _main.Dispatcher.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromDays(1);
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer.Start();
        _main.Destroying += (_, _) => _updateTimer.Stop();
        _ = CheckForUpdatesAsync();
    }
    /// <summary>手动与自动检查共用互斥状态，失败保留已知更新，不影响业务使用。</summary>
    public async Task CheckForUpdatesAsync()
    {
        if (UpdateBusy) return;
        if (!SupportsStoreUpdates) { _updateMessage = "此安装版本不支持商店内更新，请从原下载渠道获取新版。"; StatusChanged?.Invoke(); return; }
        UpdateBusy = true; _updateMessage = "正在检查更新…"; StatusChanged?.Invoke();
        try
        {
#if WINDOWS
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var updates = await StoreContextForWindow().GetAppAndOptionalStorePackageUpdatesAsync().AsTask(timeout.Token);
            UpdateAvailable = updates.Count > 0;
            _updateMessage = UpdateAvailable ? "商店有可用更新" : "当前没有可用更新";
#endif
        }
        catch (Exception) { _updateMessage = "暂时无法检查更新，请稍后重试。"; }
        finally { UpdateBusy = false; StatusChanged?.Invoke(); }
    }
    /// <summary>延后一天提醒，设置中的更新入口一直可用。</summary>
    public void SnoozeUpdate() { _updateSnoozedUntil = DateTimeOffset.UtcNow.AddDays(1); StatusChanged?.Invoke(); }
    /// <summary>安装前明确提示所有窗口保存草稿；不在后台静默关闭应用。</summary>
    public async Task InstallUpdateAsync()
    {
        if (UpdateBusy || !SupportsStoreUpdates) return;
        UpdateBusy = true; StatusChanged?.Invoke();
        try
        {
            if (!await ConfirmAsync(ClientText.T("安装更新"), ClientText.T("更新可能关闭应用，请先保存所有窗口中的草稿。现在继续？"))) return;
#if WINDOWS
            var context = StoreContextForWindow();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(timeout.Token);
            if (updates.Count == 0) { UpdateAvailable = false; _updateMessage = "当前没有可用更新"; return; }
            _updateMessage = "正在安装更新…"; StatusChanged?.Invoke();
            var result = await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
            if (result.OverallState == Windows.Services.Store.StorePackageUpdateState.Completed)
            { UpdateAvailable = false; _updateMessage = "更新已安装，请重新打开应用。"; }
            else _updateMessage = "更新未完成，可稍后重试。";
#endif
        }
        catch (Exception) { _updateMessage = "更新未完成，可稍后重试。"; }
        finally { UpdateBusy = false; StatusChanged?.Invoke(); }
    }
#if WINDOWS
    /// <summary>商店对话框关联主窗口，避免 WinUI 桌面 HWND 缺失。</summary>
    private Windows.Services.Store.StoreContext StoreContextForWindow()
    {
        var context = Windows.Services.Store.StoreContext.GetDefault();
        if (_main?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window window) throw new InvalidOperationException();
        WinRT.Interop.InitializeWithWindow.Initialize(context, WinRT.Interop.WindowNative.GetWindowHandle(window));
        return context;
    }
#endif
}
