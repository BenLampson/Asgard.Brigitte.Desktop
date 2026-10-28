using Microsoft.UI.Windowing;
using Windows.Storage.Pickers;
namespace Asgard.Brigitte.Desktop;

public sealed partial class DesktopHost
{
    private readonly HashSet<Window> _attached = [];
    private partial IPlatformTray CreateTray() => new WindowsTray(this);
    private partial void AttachPlatformWindow(Window window, DesktopViewKind kind, Window? owner)
    {
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native || !_attached.Add(window)) return;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(native);
        var appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle));
        ApplyWindowTheme(window);
        window.Created+=(_,_)=>ApplyWindowTheme(window);
        // 先记录 owner 边界，即使提交完成后源窗口立即关闭，也保留正确的居中基准。
        var placement = CaptureOwnerPlacement(owner);
        // 等 MAUI 完成原生尺寸设置后只居中一次；复用或从托盘恢复时保留用户移动的位置。
        if (kind != DesktopViewKind.Tray) window.Created += (_, _) => window.Dispatcher.Dispatch(() => CenterNewWindow(window, appWindow, placement));
        if (kind == DesktopViewKind.Main) window.Created += (_, _) => WinUI.Program.Instance?.BindActivation(() =>
            window.Dispatcher.Dispatch(() =>
            {
                if (_quitting) return;
                HidePopup();
                Show(window, true);
                NativeTray.SetForegroundWindow(handle);
            }));
        // EXE 与运行中的任务栏窗口使用同一份多尺寸女神图标；在 MAUI 初始化后再次应用。
        var iconPath = Path.Combine(AppContext.BaseDirectory, "brigid.ico");
        appWindow.SetIcon(iconPath);
        window.Created += (_, _) => window.Dispatcher.Dispatch(() =>
        {
            if (window.Handler is not null) appWindow.SetIcon(iconPath);
        });
        if (kind == DesktopViewKind.Main) appWindow.Closing += async (_, e) => { if (!_quitting) { e.Cancel = true; await RequestMainCloseAsync(); } };
        // 原生内容建立后再移除标题栏，MAUI 才会同步清除顶部预留的 32px。
        if (kind == DesktopViewKind.Tray) window.Created += (_, _) => window.Dispatcher.Dispatch(() => ConfigureTrayWindow(appWindow));
        window.Destroying += (_, _) => _attached.Remove(window);
    }
    private static void ConfigureTrayWindow(AppWindow app)
    {
        app.TitleBar.ExtendsContentIntoTitleBar = false;
        if (app.Presenter is not OverlappedPresenter presenter) return;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        presenter.IsAlwaysOnTop = true;
        // 改完窗口能力后最后清除边框，避免原生样式更新重新带回白色边缘。
        presenter.SetBorderAndTitleBar(false, false);
        app.IsShownInSwitchers = false;
        WindowsTrayAppearance.Apply(Microsoft.UI.Win32Interop.GetWindowFromWindowId(app.Id));
    }
    /// <summary>获取可见 owner 的原生像素边界与 DPI，避免混用 MAUI 逻辑坐标。</summary>
    private static (DesktopWindowBounds Bounds, DesktopWindowBounds WorkArea, double Scale)? CaptureOwnerPlacement(Window? owner)
    {
        if (owner?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) return null;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(native);
        if (!NativeTray.IsWindowVisible(handle) || NativeTray.IsIconic(handle)) return null;
        var app = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle));
        var area = DisplayArea.GetFromWindowId(app.Id, DisplayAreaFallback.Nearest).WorkArea;
        var scale = NativeTray.GetDpiForWindow(handle) / 96d;
        return (new(app.Position.X, app.Position.Y, app.Size.Width, app.Size.Height),
            new(area.X, area.Y, area.Width, area.Height), scale > 0 ? scale : 1);
    }
    /// <summary>新窗口按 owner 居中；主窗口或无可见 owner 时回退到当前屏幕工作区。</summary>
    private static void CenterNewWindow(Window window, AppWindow app,
        (DesktopWindowBounds Bounds, DesktopWindowBounds WorkArea, double Scale)? placement)
    {
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) return;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(native);
        var currentScale = NativeTray.GetDpiForWindow(handle) / 96d;
        if (currentScale <= 0) currentScale = 1;
        var display = DisplayArea.GetFromWindowId(app.Id, DisplayAreaFallback.Nearest).WorkArea;
        var area = placement?.WorkArea ?? new DesktopWindowBounds(display.X, display.Y, display.Width, display.Height);
        var scale = placement?.Scale ?? currentScale;
        // 在不同 DPI 的显示器间打开时，保持逻辑尺寸，再按目标屏幕工作区裁定最终大小。
        var width = (int)Math.Round(app.Size.Width / currentScale * scale);
        var height = (int)Math.Round(app.Size.Height / currentScale * scale);
        window.MinimumWidth = Math.Min(window.MinimumWidth, area.Width / scale);
        window.MinimumHeight = Math.Min(window.MinimumHeight, area.Height / scale);
        var bounds = DesktopWindowBounds.CenteredOnOwner(placement?.Bounds ?? area, area, width, height);
        app.MoveAndResize(new Windows.Graphics.RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }
    private void ApplyWindowTheme(Window window)
    {
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) return;
        if (native.Content is Microsoft.UI.Xaml.FrameworkElement content) content.RequestedTheme = Theme == "dark" ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
        var app = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(native)));
        var background = Theme == "dark" ? Windows.UI.Color.FromArgb(255, 20, 26, 32) : Windows.UI.Color.FromArgb(255, 238, 244, 249);
        var foreground = Theme == "dark" ? Windows.UI.Color.FromArgb(255, 238, 243, 248) : Windows.UI.Color.FromArgb(255, 36, 38, 40);
        // MAUI 使用扩展标题栏，左侧内容由 TitleBar 绘制，右侧按钮由 AppWindow 绘制。
        // 两部分必须同步设置，不能只设置原生按钮后保留 MAUI 默认强调色背景。
        if(window.TitleBar is not TitleBar { IsVisible: false }) {
            if(window.TitleBar is not TitleBar titleBar){titleBar=new TitleBar{Title=window.Title??"Brigitte",HeightRequest=32};window.TitleBar=titleBar;}
            titleBar.BackgroundColor=Color.FromArgb(Theme=="dark"?"#141A20":"#EEF4F9");
            titleBar.ForegroundColor=Color.FromArgb(Theme=="dark"?"#EEF3F8":"#242628");
            var states=new VisualStateGroup{Name="TitleActiveStates"};
            foreach(var name in new[]{"TitleBarTitleActive","TitleBarTitleInactive"}) {
                var state=new VisualState{Name=name};
                state.Setters.Add(new Setter{Property=VisualElement.BackgroundColorProperty,Value=titleBar.BackgroundColor});
                state.Setters.Add(new Setter{Property=TitleBar.ForegroundColorProperty,Value=titleBar.ForegroundColor});
                states.States.Add(state);
            }
            VisualStateManager.SetVisualStateGroups(titleBar,new VisualStateGroupList{states});
        }
        app.TitleBar.BackgroundColor=background;app.TitleBar.ForegroundColor=foreground;
        app.TitleBar.InactiveBackgroundColor=background;app.TitleBar.InactiveForegroundColor=foreground;
        app.TitleBar.ButtonBackgroundColor=background;app.TitleBar.ButtonForegroundColor=foreground;
        app.TitleBar.ButtonInactiveBackgroundColor=background;app.TitleBar.ButtonInactiveForegroundColor=foreground;
        app.TitleBar.ButtonHoverBackgroundColor=Theme=="dark"?Windows.UI.Color.FromArgb(255,48,58,68):Windows.UI.Color.FromArgb(255,220,232,244);
        app.TitleBar.ButtonPressedBackgroundColor=Theme=="dark"?Windows.UI.Color.FromArgb(255,65,76,88):Windows.UI.Color.FromArgb(255,203,221,235);
        app.TitleBar.ButtonHoverForegroundColor=foreground;app.TitleBar.ButtonPressedForegroundColor=foreground;
        if (window.TitleBar is TitleBar { IsVisible: false }) WindowsTrayAppearance.Apply(WinRT.Interop.WindowNative.GetWindowHandle(native));
    }
    private partial void ApplyPlatformTheme() { foreach (var window in _windows.Values) ApplyWindowTheme(window); }
    private partial void PositionPopup(Window window,double x,double y)
    {
        if(window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native)return;
        var app=AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(native)));
        ConfigureTrayWindow(app);
        var area=DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32((int)x,(int)y),DisplayAreaFallback.Nearest).WorkArea;
        app.Move(new Windows.Graphics.PointInt32(Math.Clamp((int)x-app.Size.Width,area.X,Math.Max(area.X,area.X+area.Width-app.Size.Width)),Math.Clamp((int)y-app.Size.Height-10,area.Y,Math.Max(area.Y,area.Y+area.Height-app.Size.Height))));
    }
    private partial async Task<bool> SavePlatformAsync(string fileName, byte[] bytes)
    {
        var picker = new FileSavePicker { SuggestedFileName = fileName };
        var extension = Path.GetExtension(fileName); if (string.IsNullOrEmpty(extension)) extension = ".bin";
        picker.FileTypeChoices.Add(ClientText.T("附件"), new List<string> { extension });
        var native = (Microsoft.UI.Xaml.Window)ActivePage.Window.Handler!.PlatformView!;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(native));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return false;
        await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
        return true;
    }
}
