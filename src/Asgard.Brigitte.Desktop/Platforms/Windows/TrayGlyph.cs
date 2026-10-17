using System.Runtime.InteropServices;
namespace Asgard.Brigitte.Desktop;
/// <summary>将同一徽记交给 Win32，不生成临时 PNG 或 ICO 文件。</summary>
internal static class TrayGlyph
{
    /// <summary>返回由调用方销毁的 HICON。</summary>
    internal static nint Create(bool light, bool unread = false)
    {
        const int size = 32; var rgba = BrandPixels.Render(light); var bgra = new byte[rgba.Length];
        // 未读圆点保持静止，使用与界面相同的品牌蓝。
        if (unread)
            for (var y = 0; y < 12; y++) for (var x = 20; x < 32; x++)
                if ((x - 26) * (x - 26) + (y - 6) * (y - 6) <= 25)
                { var index = (y * size + x) * 4; rgba[index] = 40; rgba[index + 1] = 104; rgba[index + 2] = 212; rgba[index + 3] = 255; }
        for (var i = 0; i < rgba.Length; i += 4) { bgra[i] = rgba[i + 2]; bgra[i + 1] = rgba[i + 1]; bgra[i + 2] = rgba[i]; bgra[i + 3] = rgba[i + 3]; }
        return CreateIcon(0, size, size, 1, 32, new byte[size * size / 8], bgra);
    }
    [DllImport("user32")] private static extern nint CreateIcon(nint instance, int width, int height, byte planes, byte bitsPerPixel, byte[] mask, byte[] color);
}
