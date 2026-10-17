namespace Asgard.Brigitte.Desktop.UI;
/// <summary>共享视图使用原生窗口和系统能力，不在 JS 中保存凭据。</summary>
public interface IClientHost
{
    /// <summary>用户选择的主题模式。</summary>
    string ThemePreference { get; }
    /// <summary>当前日夜模式。</summary>
    string Theme { get; }
    /// <summary>主窗口关闭时继续询问、最小化托盘或退出程序。</summary>
    string CloseBehavior => "ask";
    void SetCloseBehavior(string behavior) { }
    /// <summary>由原生应用元数据提供的版本。</summary>
    string AppVersion => "";
    /// <summary>商店登记后提供的公开政策与支持页面，未配置时不展示无效链接。</summary>
    string PrivacyPolicyUrl => "";
    string SupportUrl => "";
    /// <summary>实时连接状态，由原生共享连接提供。</summary>
    string RealtimeStatus => "未连接";
    /// <summary>启动对账完成后才开放工作区；断网保留已同步内容只读。</summary>
    bool SyncReady => false;
    bool CanWrite => false;
    bool SyncHasManifest => false;
    int SyncReceived => 0;
    int SyncTotal => 0;
    int PendingCommands => 0;
    string NotificationText => "";
    bool ShowNotification(DesktopView view) => false;
    void DismissNotification() { }
    IReadOnlyList<Asgard.Brigitte.Desktop.Core.SyncNotice> RecentChanges => [];
    int UnreadChanges => 0;
    void MarkChangesRead() { }
    void MarkChangeRead(string id) { }
    /// <summary>每台客户端稳定 Worker ID。</summary>
    string WorkerId { get; }
    /// <summary>托盘或项目导航明确选择的项目。</summary>
    string? RequestedProjectId { get; }
    /// <summary>跨窗口状态通知。</summary>
    event Action? Changed;
    /// <summary>仅重新显示连接状态，不重载正在编辑的数据。</summary>
    event Action? StatusChanged;
    /// <summary>主题切换同时作用于原生窗口和所有视图。</summary>
    void SetTheme(string theme);
    /// <summary>打开独立窗口。</summary>
    Task OpenAsync(DesktopView view);
    /// <summary>原生宿主按发起窗口居中；不管理原生窗口的宿主沿用普通导航。</summary>
    Task OpenAsync(DesktopView view, DesktopView? owner) => OpenAsync(view);
    /// <summary>关闭当前独立窗口。</summary>
    void Close(DesktopView view);
    /// <summary>保存后刷新父看板，同时保留筛选和滚动位置。</summary>
    void PublishChange();
    /// <summary>系统剪贴板复制完整原始值。</summary>
    Task CopyAsync(string text);
    /// <summary>既有危险操作的明确确认。</summary>
    Task<bool> ConfirmAsync(string title, string message);
    /// <summary>系统文件选择器。</summary>
    Task<IReadOnlyList<PickedFile>> PickFilesAsync();
    /// <summary>将用户选择的附件保存到指定位置。</summary>
    Task SaveAsync(string fileName, byte[] content);
    /// <summary>在系统浏览器打开明确的外链。</summary>
    Task OpenExternalAsync(string url);
    /// <summary>退出整个客户端。</summary>
    void Quit();
}
