namespace Asgard.Brigitte.Desktop;
/// <summary>将 Pen 圣火女神主标的抗锯齿遮罩转换为系统托盘像素。</summary>
internal static class BrandPixels
{
    private static readonly byte[] Alpha = LoadAlpha();
    /// <summary>32 像素 RGBA 单色图标，颜色跟随系统任务栏明暗模式。</summary>
    internal static byte[] Render(bool light)
    {
        var bytes = new byte[32 * 32 * 4];
        for (var pixel = 0; pixel < Alpha.Length; pixel++)
        {
            var offset = pixel * 4;
            bytes[offset] = bytes[offset + 1] = bytes[offset + 2] = (byte)(light ? 245 : 40);
            bytes[offset + 3] = Alpha[pixel];
        }
        return bytes;
    }
    /// <summary>遮罩在构建资源时由同一份 SVG 生成，不依赖运行时图形库。</summary>
    private static byte[] LoadAlpha()
    {
        using var stream = typeof(BrandPixels).Assembly.GetManifestResourceStream("Asgard.Brigitte.Desktop.TrayAlpha")!;
        var alpha = new byte[32 * 32];
        stream.ReadExactly(alpha);
        return alpha;
    }
}
