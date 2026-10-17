namespace Asgard.Brigitte.Desktop.Core;

/// <summary>原生像素坐标中的窗口边界，支持副屏负坐标及小屏工作区。</summary>
public readonly record struct DesktopWindowBounds(int X, int Y, int Width, int Height)
{
    /// <summary>在扣除任务栏的显示器工作区内居中，窗口尺寸不超出该工作区。</summary>
    public static DesktopWindowBounds Centered(int x, int y, int availableWidth, int availableHeight, int width, int height)
    {
        var fitWidth = Math.Clamp(width, 1, Math.Max(1, availableWidth));
        var fitHeight = Math.Clamp(height, 1, Math.Max(1, availableHeight));
        return new(x + (availableWidth - fitWidth) / 2, y + (availableHeight - fitHeight) / 2, fitWidth, fitHeight);
    }
    /// <summary>按 owner 的中心定位，并将完整窗口限制在 owner 所在显示器的工作区内。</summary>
    public static DesktopWindowBounds CenteredOnOwner(DesktopWindowBounds owner, DesktopWindowBounds workArea, int width, int height)
    {
        var fitWidth = Math.Clamp(width, 1, Math.Max(1, workArea.Width));
        var fitHeight = Math.Clamp(height, 1, Math.Max(1, workArea.Height));
        var x = owner.X + (owner.Width - fitWidth) / 2;
        var y = owner.Y + (owner.Height - fitHeight) / 2;
        return new(Math.Clamp(x, workArea.X, workArea.X + Math.Max(0, workArea.Width - fitWidth)),
            Math.Clamp(y, workArea.Y, workArea.Y + Math.Max(0, workArea.Height - fitHeight)), fitWidth, fitHeight);
    }
}
