using System.Runtime.InteropServices;
namespace Asgard.Brigitte.Desktop;

/// <summary>托盘浮层由页面绘制细边线，避免 Windows 11 的亮色窗口轮廓覆盖深色设计。</summary>
internal static class WindowsTrayAppearance
{
    private const uint BorderColor = 34;
    private const uint CornerPreference = 33;
    private const uint NoBorder = 0xFFFFFFFE;
    private const uint RoundCorners = 2;
    private const int StyleIndex = -16;
    private const long FrameStyles = 0x00C40000; // WS_BORDER、WS_DLGFRAME、WS_THICKFRAME。
    private const uint RefreshFrame = 0x0037; // FRAMECHANGED、NOSIZE、NOMOVE、NOZORDER、NOACTIVATE。

    /// <summary>仅覆盖本应用的托盘窗口；旧系统继续使用无标题栏、无边框的 Presenter。</summary>
    internal static void Apply(nint handle)
    {
        // MAUI 在非可缩放窗口上仍可能保留 WS_DLGFRAME；它绘制的白框不受 DWM 颜色控制。
        var style = GetWindowLongPtr(handle, StyleIndex).ToInt64();
        if ((style & FrameStyles) != 0)
        {
            _ = SetWindowLongPtr(handle, StyleIndex, (nint)(style & ~FrameStyles));
            _ = SetWindowPos(handle, 0, 0, 0, 0, 0, RefreshFrame);
        }
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var border = NoBorder;
        var corners = RoundCorners;
        _ = DwmSetWindowAttribute(handle, BorderColor, ref border, sizeof(uint));
        _ = DwmSetWindowAttribute(handle, CornerPreference, ref corners, sizeof(uint));
    }

    /// <summary>Windows 官方窗口外观接口，失败时不阻断托盘打开。</summary>
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint handle, uint attribute, ref uint value, uint size);

    /// <summary>读取和移除托盘窗口的非客户区边框，不改变其他窗口样式。</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint handle, int index, nint value);
    /// <summary>让 Windows 重新计算边界，保持窗口位置、尺寸和焦点。</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);
}
