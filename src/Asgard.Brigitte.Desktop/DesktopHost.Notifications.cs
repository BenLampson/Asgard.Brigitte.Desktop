namespace Asgard.Brigitte.Desktop;

/// <summary>落库后的轻提示；不闪窗口、不移动焦点，前台仅当前窗口显示一次。</summary>
public sealed partial class DesktopHost
{
    private DesktopView? _activeView, _notificationView;
    private readonly List<SyncNotice> _pendingNotices = [];
    private int _notificationVersion;
    private long _notificationIdentity = -1;
    private bool _notificationScheduled;
    private SyncNotice[] _visibleNotices = [];
    public string NotificationText => string.Join("\n\n", _visibleNotices.Select(x => x.DisplayMessage));
    public IReadOnlyList<SyncNotice> RecentChanges => _realtime.Notices;
    public int UnreadChanges => RecentChanges.Count;
    public void MarkChangesRead() { _realtime.MarkNoticesRead(); DismissNotification(); RefreshNotices(); }
    public void MarkChangeRead(string id) { _realtime.MarkNoticeRead(id); RefreshNotices(); }
    /// <summary>已读移除后同步更新角标、列表和弹窗尺寸。</summary>
    private void RefreshNotices()
    {
        if (_popup is not null)
        {
            ApplyTraySize(_popup);
            if (_activeView?.Kind == DesktopViewKind.Tray) PositionPopup(_popup, _popupAnchorX, _popupAnchorY);
        }
        _tray?.Refresh(); StatusChanged?.Invoke();
    }
    public bool ShowNotification(DesktopView view) => _notificationView == view && NotificationText.Length > 0;
    public void DismissNotification() { _visibleNotices = []; _notificationView = null; StatusChanged?.Invoke(); }
    private void NotifyCommitted(IReadOnlyList<SyncNotice> notices)
    {
        var identity = _session.IdentityVersion;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (identity != _session.IdentityVersion || !_session.IsAuthenticated) return;
            if (_notificationIdentity != identity) { ClearNotifications(); _notificationIdentity = identity; }
            _pendingNotices.AddRange(notices);
            if (_notificationScheduled) return;
            _notificationScheduled = true;
            await Task.Delay(600);
            if (identity != _session.IdentityVersion) return;
            _notificationScheduled = false;
            // 合并期间已经点过已读的通知不再弹出；系统横幅消失本身不等于用户已读。
            var unread = RecentChanges.Select(x => x.Id).ToHashSet();
            var current = _pendingNotices.Where(x => unread.Contains(x.Id)).DistinctBy(x => x.Id).ToArray();
            _pendingNotices.Clear();
            if (current.Length == 0) return;
            _visibleNotices = current;
            _notificationView = _activeView?.Kind == DesktopViewKind.Tray ? null : _activeView;
            if (_activeView is null) _tray?.Notify(ClientText.T("Brigitte · 工作区动态"), NativeNoticeText(current));
            var version = ++_notificationVersion;
            RefreshNotices();
            await Task.Delay(8000);
            if (version == _notificationVersion) DismissNotification();
        });
    }
    /// <summary>系统横幅只显示动态数量，项目、人名和 BUG 标题留在应用内，避免锁屏泄露协作信息。</summary>
    private static string NativeNoticeText(SyncNotice[] notices) => ClientText.T("有 {0} 条工作区动态，请打开 Asgard Brigitte 查看。", notices.Length);
    private void ClearNotifications()
    {
        _notificationVersion++; _notificationScheduled = false; _pendingNotices.Clear();
        _visibleNotices = []; _notificationView = null;
    }
}
