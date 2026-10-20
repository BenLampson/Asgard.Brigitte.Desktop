using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
namespace Asgard.Brigitte.Desktop;
/// <summary>Shell_NotifyIcon 托盘，仅点击展开，包含 Explorer 重启恢复。</summary>
internal sealed class WindowsTray : IPlatformTray
{
    private readonly DesktopHost _host;
    private readonly NativeTray.WndProc _procedure;
    private readonly string _className = "Brigitte.Tray." + Guid.NewGuid().ToString("N");
    private nint _window, _icon;
    private NativeTray.NotifyIconData _data;
    private readonly uint _taskbarCreated;
    private bool _notificationsRegistered;
    /// <summary>系统图标添加成功后才启用隐藏主窗口。</summary>
    public bool IsAvailable { get; private set; }
    internal WindowsTray(DesktopHost host)
    {
        _host = host; _procedure = WndProc;
        var cls = new NativeTray.WindowClass { Procedure = Marshal.GetFunctionPointerForDelegate(_procedure), Instance = NativeTray.GetModuleHandle(null), ClassName = _className };
        if (NativeTray.RegisterClass(ref cls) == 0) return;
        _window = NativeTray.CreateWindowEx(0, _className, "", 0, 0, 0, 0, 0, 0, 0, cls.Instance, 0);
        _taskbarCreated = NativeTray.RegisterWindowMessage("TaskbarCreated");
        _icon = TrayGlyph.Create(UseLightGlyph());
        _data = new() { Size = (uint)Marshal.SizeOf<NativeTray.NotifyIconData>(), Window = _window, Id = 1, Flags = 7, Callback = 0x8001, Icon = _icon, Tip = ClientText.T("Brigitte · BUG 管理器"), Info = "", InfoTitle = "" };
        IsAvailable = NativeTray.Shell_NotifyIcon(0, ref _data);
        try
        {
            AppNotificationManager.Default.NotificationInvoked += NotificationInvoked;
            AppNotificationManager.Default.Register();
            _notificationsRegistered = true;
        }
        catch (Exception) { AppNotificationManager.Default.NotificationInvoked -= NotificationInvoked; }
    }
    /// <summary>项目数量进入提示，主题变更更新单色徽记。</summary>
    public void Refresh()
    {
        if (_window == 0) return;
        var old = _icon; _icon = TrayGlyph.Create(UseLightGlyph(), _host.UnreadChanges > 0); _data.Icon = _icon;
        _data.Tip = _host.IsAuthenticated ? ClientText.T("Asgard Brigitte · {0} 个项目 · {1} 个未解决 BUG", _host.Projects.Count, _host.Projects.Sum(p => _host.ProjectCount(p.Id))) : ClientText.T("Brigitte · 请先登录");
        if (_data.Tip.Length > 127) _data.Tip = _data.Tip[..127];
        IsAvailable = NativeTray.Shell_NotifyIcon(IsAvailable ? 1u : 0u, ref _data); if (old != 0) NativeTray.DestroyIcon(old);
    }
    private nint WndProc(nint window, uint message, nint wParam, nint lParam)
    {
        if (message == 0x1A) { MainThread.BeginInvokeOnMainThread(() => { _host.RefreshSystemLanguage(); Refresh(); }); return 0; }
        if (message == _taskbarCreated) { IsAvailable = false; Refresh(); return 0; }
        if (message == 0x8001)
        {
            var mouse = (uint)lParam;
            if (mouse is 0x0201 or 0x0204)
            {
                NativeTray.GetCursorPos(out var point);
                MainThread.BeginInvokeOnMainThread(() => _host.ShowPopup(point.X, point.Y));
            }
            if (mouse is 0x0203 or 0x0405) MainThread.BeginInvokeOnMainThread(async () => await _host.OpenAsync(new(DesktopViewKind.Main)));
            return 0;
        }
        return NativeTray.DefWindowProc(window, message, wParam, lParam);
    }
    /// <summary>应用通知发送完整正文，绕过旧托盘气泡的 255 字符缓冲区；系统控制横幅排版。</summary>
    public void Notify(string title, string message)
    {
        if (!_notificationsRegistered) return;
        try
        {
            var parts = NotificationParts.Split(message);
            for (var i = 0; i < parts.Count; i++)
                AppNotificationManager.Default.Show(new AppNotificationBuilder()
                    .AddText(parts.Count == 1 ? title : $"{title} ({i + 1}/{parts.Count})").AddText(parts[i]).BuildNotification());
        }
        catch (Exception) { /* 通知失败不影响同步；完整正文仍保留在未读动态中。 */ }
    }
    /// <summary>点击系统通知打开未读动态，完整内容不受横幅行数限制。</summary>
    private void NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        => MainThread.BeginInvokeOnMainThread(() => { NativeTray.GetCursorPos(out var point); _host.ShowPopup(point.X, point.Y); });
    /// <summary>托盘按系统任务栏外观选择单色，独立于用户选择的内容主题。</summary>
    private bool UseLightGlyph()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value ? value == 0 : _host.Theme == "dark";
    }
    /// <summary>显示时仅还原最小化窗口，保留当前尺寸与最大化状态。</summary>
    public void Show(Window window, bool visible)
    {
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) return;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(native);
        var app = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle));
        if (visible)
        {
            // SW_RESTORE 也会取消最大化，因此只对最小化窗口调用。
            if (NativeTray.IsIconic(handle)) NativeTray.ShowWindow(handle, 9);
            app.Show();
            native.Activate();
        }
        else app.Hide();
    }
    /// <summary>移除图标和隐藏消息窗口，不留下幽灵托盘项。</summary>
    public void Dispose() { if (_notificationsRegistered) { AppNotificationManager.Default.NotificationInvoked -= NotificationInvoked; AppNotificationManager.Default.Unregister(); _notificationsRegistered = false; } if (_window == 0) return; NativeTray.Shell_NotifyIcon(2, ref _data); if (_icon != 0) NativeTray.DestroyIcon(_icon); NativeTray.DestroyWindow(_window); NativeTray.UnregisterClass(_className, NativeTray.GetModuleHandle(null)); _window = 0; IsAvailable = false; GC.KeepAlive(_procedure); }
}
