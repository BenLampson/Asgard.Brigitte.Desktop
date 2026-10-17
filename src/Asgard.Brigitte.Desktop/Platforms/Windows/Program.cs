using Microsoft.UI.Dispatching;

namespace Asgard.Brigitte.Desktop.WinUI;

/// <summary>先完成进程互斥，再初始化 MAUI，重复启动不创建会话、数据库或窗口。</summary>
internal static class Program
{
    internal static WindowsSingleInstance? Instance { get; private set; }

    /// <summary>保留同步 STA 入口；只有首个进程进入窗口循环。</summary>
    [STAThread]
    private static void Main()
    {
        using var instance = new WindowsSingleInstance();
        if (!instance.IsPrimary)
        {
            // 此时尚未创建 UI 同步上下文；等待有超时的管道回执，不涉及 COM 或窗口调用。
            instance.ActivateExistingAsync().GetAwaiter().GetResult();
            return;
        }
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (!WindowsRuntimePrerequisites.EnsureWebView2()) return;
        Instance = instance;
        Microsoft.UI.Xaml.Application.Start(launch =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        Instance = null;
    }
}
