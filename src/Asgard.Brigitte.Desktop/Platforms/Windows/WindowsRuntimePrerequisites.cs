using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace Asgard.Brigitte.Desktop;

/// <summary>首次安装缺少 WebView2 时提供明确说明，不让用户停留在空白窗口。</summary>
internal static class WindowsRuntimePrerequisites
{
    /// <summary>仅首个进程检查依赖；用户确认后打开微软官方安装页面，不自动安装或提权。</summary>
    internal static bool EnsureWebView2()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(CoreWebView2Environment.GetAvailableBrowserVersionString())) return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is COMException { HResult: unchecked((int)0x80070002) or unchecked((int)0x80040154) })
        { /* WinUI 使用 WinRT 绑定，缺少 Runtime 时返回文件或 COM 注册错误。 */ }
        var answer = MessageBox(0,
            ClientText.T("Brigitte 需要 Microsoft Edge WebView2 Runtime 才能显示界面。\n\n请安装后重新打开 Brigitte。是否打开微软官方下载页面？"),
            ClientText.T("Brigitte · 缺少运行组件"), 0x00000004 | 0x00000040);
        if (answer == 6)
        {
            try { Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                MessageBox(0, ClientText.T("无法打开浏览器，请在微软官网搜索并安装 Microsoft Edge WebView2 Runtime。"), "Brigitte", 0x00000040);
            }
        }
        return false;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint owner, string message, string title, uint type);
}
