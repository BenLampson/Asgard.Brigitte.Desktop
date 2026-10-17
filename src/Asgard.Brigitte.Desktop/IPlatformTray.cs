namespace Asgard.Brigitte.Desktop;
/// <summary>各平台的真实系统托盘适配。</summary>
internal interface IPlatformTray : IDisposable
{
    /// <summary>初始化成功才允许关闭主窗口后隐藏。</summary>
    bool IsAvailable { get; }
    /// <summary>更新主题图标及项目菜单。</summary>
    void Refresh();
    /// <summary>后台同步完成后展示系统轻提示。</summary>
    void Notify(string title, string message) { }
    /// <summary>隐藏或激活指定原生窗口。</summary>
    void Show(Window window, bool visible);
}
