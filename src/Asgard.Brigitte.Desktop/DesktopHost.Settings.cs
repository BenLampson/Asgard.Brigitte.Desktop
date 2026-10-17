namespace Asgard.Brigitte.Desktop;

/// <summary>设备级设置与主窗口关闭编排，独立于登录和业务同步。</summary>
public sealed partial class DesktopHost
{
    private bool _closeRequestPending;
    public string CloseBehavior => _closePreference.Behavior;
    public string AppVersion => AppInfo.Current.VersionString;
    public string PrivacyPolicyUrl => StoreLink("Brigitte.PrivacyPolicyUrl") is { Length: > 0 } custom ? custom :
        "https://github.com/BenLampson/Asgard.Brigitte.Desktop/blob/main/" + (Language == "zh-CN" ? "PRIVACY.zh-CN.md" : "PRIVACY.md");
    public string SupportUrl => StoreLink("Brigitte.SupportUrl") is { Length: > 0 } custom ? custom :
        "https://github.com/BenLampson/Asgard.Brigitte.Desktop/issues/new/choose";
    /// <summary>正式政策地址通过构建元数据注入，不推测域名或展示尚不存在的页面。</summary>
    private static string StoreLink(string key)
    {
        var value = typeof(DesktopHost).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(item => item.Key == key)?.Value;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" ? uri.AbsoluteUri : "";
    }
    /// <summary>设置窗口可立即恢复询问；与关闭对话框共用同一持久化入口。</summary>
    public void SetCloseBehavior(string behavior)
    {
        _closePreference.Set(behavior);
        StatusChanged?.Invoke();
    }
    /// <summary>只拦截主窗口的关闭；连续点击不叠加对话框，显式退出不会再次询问。</summary>
    internal async Task RequestMainCloseAsync()
    {
        if (_quitting || _closeRequestPending) return;
        _closeRequestPending = true;
        try
        {
            var action = CloseBehavior;
            var remember = false;
            if (action == "ask" || (action == "tray" && _tray?.IsAvailable != true))
                (action, remember) = await ChooseMainCloseAsync(_tray?.IsAvailable == true);
            if (action is not ("tray" or "exit")) return;
            // 托盘丢失时保留可见窗口，不能把用户隐藏到无法恢复的后台。
            if (action == "tray" && _tray?.IsAvailable != true) return;
            if (remember) SetCloseBehavior(action);
            if (action == "exit") Quit();
            else if (_main is not null) { HidePopup(); Show(_main, false); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_main?.Page is { } page)
                await page.DisplayAlertAsync(ClientText.T("未能保存关闭偏好"), ClientText.T("设置文件暂时无法写入，窗口已保持打开。你可以不勾选“以后不再提示”再试一次。"), ClientText.T("知道了"));
        }
        catch (InvalidOperationException)
        {
            // 主窗口已有原生对话框时保持它可见，待用户处理后可再次关闭。
        }
        finally { _closeRequestPending = false; }
    }
    /// <summary>原生对话框同时返回本次动作和是否记住，不保存取消操作。</summary>
    private partial Task<(string Action, bool Remember)> ChooseMainCloseAsync(bool canUseTray);
}
