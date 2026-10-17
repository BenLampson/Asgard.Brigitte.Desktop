using Foundation;
namespace Asgard.Brigitte.Desktop;
/// <summary>Mac Catalyst 生命周期入口。</summary>
[Register("AppDelegate")]
public sealed class AppDelegate : MauiUIApplicationDelegate
{
    /// <summary>使用相同业务与会话装配。</summary>
    protected override MauiApp CreateMauiApp()=>MauiProgram.CreateMauiApp();
}
