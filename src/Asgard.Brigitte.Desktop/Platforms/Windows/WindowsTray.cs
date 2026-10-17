using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
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
        _data = new() { Size = (uint)Marshal.SizeOf<NativeTray.NotifyIconData>(), Window = _window, Id = 1, Flags = 7, Callback = 0x8001, Icon = _icon, Tip = "Brigitte · BUG 管理器", Info = "", InfoTitle = "" };
        IsAvailable = NativeTray.Shell_NotifyIcon(0, ref _data);
    }
    /// <summary>项目数量进入提示，主题变更更新单色徽记。</summary>
    public void Refresh()
    {
        if (_window == 0) return;
        var old = _icon; _icon = TrayGlyph.Create(UseLightGlyph(), _host.UnreadChanges > 0); _data.Icon = _icon;
        _data.Tip = _host.IsAuthenticated ? "Brigitte · " + _host.Projects.Count + " 个项目 · " + _host.Projects.Sum(p => _host.ProjectCount(p.Id)) + " 个未解决 BUG" : "Brigitte · 请先登录";
        IsAvailable = NativeTray.Shell_NotifyIcon(IsAvailable ? 1u : 0u, ref _data); if (old != 0) NativeTray.DestroyIcon(old);
    }
    private nint WndProc(nint window, uint message, nint wParam, nint lParam)
    {
        if (message == 0x1A) { Refresh(); return 0; }
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
    /// <summary>使用系统托盘通知，系统的免打扰偏好继续生效。</summary>
    public void Notify(string title, string message)
    {
        if (!IsAvailable) return;
        _data.Flags = 16; _data.InfoTitle = title; _data.Info = message; _data.InfoFlags = 1;
        NativeTray.Shell_NotifyIcon(1, ref _data);
        _data.Flags = 7; _data.InfoTitle = ""; _data.Info = "";
    }
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
    public void Dispose() { if (_window == 0) return; NativeTray.Shell_NotifyIcon(2, ref _data); if (_icon != 0) NativeTray.DestroyIcon(_icon); NativeTray.DestroyWindow(_window); NativeTray.UnregisterClass(_className, NativeTray.GetModuleHandle(null)); _window = 0; IsAvailable = false; GC.KeepAlive(_procedure); }
}
