namespace Asgard.Brigitte.Desktop.WinUI;
/// <summary>Windows App SDK 启动入口。</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>加载 WinUI 资源。</summary>
    public App() => InitializeComponent();
    /// <summary>使用共享 MAUI 装配。</summary>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
