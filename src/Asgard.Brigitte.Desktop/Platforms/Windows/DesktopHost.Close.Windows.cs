using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Asgard.Brigitte.Desktop;

/// <summary>使用当前主窗口的 WinUI 对话框，支持键盘取消和记住关闭选择。</summary>
public sealed partial class DesktopHost
{
    private partial async Task<(string Action, bool Remember)> ChooseMainCloseAsync(bool canUseTray)
    {
        var root = (_main?.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.Content as FrameworkElement;
        if (root?.XamlRoot is null) return ("cancel", false);
        var remember = new Microsoft.UI.Xaml.Controls.CheckBox { Content = ClientText.T("以后不再提示，记住这次选择") };
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(new TextBlock
        {
            Text = canUseTray ? ClientText.T("退出程序会停止后台同步和通知。最小化到托盘会继续运行，可点击托盘图标重新打开。") : ClientText.T("系统托盘暂不可用。你可以退出程序，或取消并保留窗口。"),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(remember);
        content.Children.Add(new TextBlock { Text = ClientText.T("可随时在“设置 → 普通设置”中改回继续询问。"), TextWrapping = TextWrapping.Wrap, Opacity = 0.65, FontSize = 12 });
        var dialog = new ContentDialog
        {
            XamlRoot = root.XamlRoot,
            RequestedTheme = Theme == "dark" ? ElementTheme.Dark : ElementTheme.Light,
            Title = ClientText.T("关闭 Brigitte"),
            Content = content,
            PrimaryButtonText = ClientText.T("最小化到托盘"),
            IsPrimaryButtonEnabled = canUseTray,
            SecondaryButtonText = ClientText.T("退出程序"),
            CloseButtonText = ClientText.T("取消"),
            DefaultButton = ContentDialogButton.Close
        };
        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => ("tray", remember.IsChecked == true),
            ContentDialogResult.Secondary => ("exit", remember.IsChecked == true),
            _ => ("cancel", false)
        };
    }
}
