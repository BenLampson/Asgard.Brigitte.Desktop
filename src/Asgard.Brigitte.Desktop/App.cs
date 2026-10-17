namespace Asgard.Brigitte.Desktop;
/// <summary>桌面窗口的唯一原生应用入口。</summary>
public sealed class App : Application
{
    private readonly DesktopHost _host;
    private readonly SessionMaintenance _maintenance;
    private readonly DesktopRealtime _realtime;
    /// <summary>通过 DI 获取窗口与托盘协调服务。</summary>
    public App(DesktopHost host, SessionMaintenance maintenance, DesktopRealtime realtime) { _host = host; _maintenance = maintenance; _realtime = realtime; UserAppTheme = host.ThemePreference switch { "light" => AppTheme.Light, "dark" => AppTheme.Dark, _ => AppTheme.Unspecified }; RequestedThemeChanged += (_, _) => host.NotifyThemeChanged(); }
    /// <summary>创建主窗口，其余窗口均由用户操作单独打开。</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = _host.CreateMainWindow();
        window.Created += (_, _) => { _maintenance.Start(); _realtime.Start(); };
        window.Destroying += (_, _) => { _maintenance.Dispose(); _realtime.Dispose(); };
        return window;
    }
}
