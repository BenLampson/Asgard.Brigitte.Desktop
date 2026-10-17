using System.Runtime.InteropServices;
namespace Asgard.Brigitte.Desktop;
/// <summary>仅系统托盘所需的 Win32 ABI。</summary>
internal static class NativeTray
{
    internal delegate nint WndProc(nint window, uint message, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct WindowClass { internal uint Style; internal nint Procedure; internal int ClassExtra, WindowExtra; internal nint Instance, Icon, Cursor, Background; internal string? MenuName; internal string ClassName; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct NotifyIconData { internal uint Size; internal nint Window; internal uint Id, Flags, Callback; internal nint Icon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Tip; internal uint State, StateMask; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string Info; internal uint Timeout; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string InfoTitle; internal uint InfoFlags; internal Guid Guid; internal nint BalloonIcon; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct IconIdentifier { internal uint Size; internal nint Window; internal uint Id; internal Guid Guid; }
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern ushort RegisterClass(ref WindowClass value);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern nint CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32")] internal static extern nint DefWindowProc(nint window, uint msg, nint w, nint l);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("shell32", CharSet = CharSet.Unicode)] internal static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32")] internal static extern bool DestroyWindow(nint window);
    [DllImport("user32")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32")] internal static extern bool ShowWindow(nint window, int command);
    [DllImport("user32")] internal static extern bool IsIconic(nint window);
    [DllImport("user32")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("shell32")] internal static extern int Shell_NotifyIconGetRect(ref IconIdentifier identifier, out Rect rect);
}
