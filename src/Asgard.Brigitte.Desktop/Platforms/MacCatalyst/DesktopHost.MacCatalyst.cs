namespace Asgard.Brigitte.Desktop;
public sealed partial class DesktopHost
{
    private partial IPlatformTray CreateTray()=>new MacTray(this);
    private partial Task<(string Action,bool Remember)> ChooseMainCloseAsync(bool canUseTray)=>Task.FromResult(MacTray.PromptClose(canUseTray));
    private partial void PositionPopup(Window window,double x,double y){window.X=Math.Max(0,x-window.Width);window.Y=Math.Max(0,y-window.Height);}
    private partial void ApplyPlatformTheme(){foreach(var window in _windows.Values)if(window.Handler?.PlatformView is UIKit.UIWindow native)native.OverrideUserInterfaceStyle=ThemePreference switch{"light"=>UIKit.UIUserInterfaceStyle.Light,"dark"=>UIKit.UIUserInterfaceStyle.Dark,_=>UIKit.UIUserInterfaceStyle.Unspecified};}
    private partial void AttachPlatformWindow(Window window,DesktopViewKind kind,Window? owner)
    {
        window.Created+=(_,_)=>MainThread.BeginInvokeOnMainThread(()=>MacTray.Attach(window.Title,kind==DesktopViewKind.Main));
    }
    private partial Task SavePlatformAsync(string fileName,byte[] bytes)=>MainThread.InvokeOnMainThreadAsync(()=>MacTray.Save(fileName,bytes));
}
