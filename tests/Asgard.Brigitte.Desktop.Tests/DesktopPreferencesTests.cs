using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证主题跨实例保存和不同显示器工作区的首次居中。</summary>
public sealed class DesktopPreferencesTests
{
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("system")]
    public void Theme_survives_restart_and_does_not_follow_legacy_settings_after_migration(string mode)
    {
        var directory = Directory.CreateTempSubdirectory("brigitte-theme-test-");
        try
        {
            var path = Path.Combine(directory.FullName, "theme.json");
            var initial = new ThemePreferenceStore(path, () => "dark");
            Assert.Equal("dark", initial.Mode);
            initial.Set(mode);
            var restarted = new ThemePreferenceStore(path, () => throw new InvalidOperationException("已迁移的主题不得重新读取旧配置。"));
            Assert.Equal(mode, restarted.Mode);
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Throws<ArgumentException>(() => restarted.Set("invalid"));
            Assert.Equal(mode, new ThemePreferenceStore(path).Mode);
        }
        finally { File.Delete(Path.Combine(directory.FullName, "theme.json")); directory.Delete(); }
    }

    [Theory]
    [InlineData(0, 0, 1920, 1040, 1440, 900, 240, 70, 1440, 900)]
    [InlineData(-2560, 0, 2560, 1400, 1440, 900, -2000, 250, 1440, 900)]
    [InlineData(1920, 32, 1280, 688, 2160, 1350, 1920, 32, 1280, 688)]
    public void Startup_window_centers_inside_work_area_in_native_pixels(int x, int y, int availableWidth, int availableHeight,
        int width, int height, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(new DesktopWindowBounds(expectedX, expectedY, expectedWidth, expectedHeight),
            DesktopWindowBounds.Centered(x, y, availableWidth, availableHeight, width, height));
    }
}
