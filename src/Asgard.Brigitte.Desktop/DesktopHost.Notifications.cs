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
    public IReadOnlyList<SyncNotice> RecentChanges => _session.IsLocal ? [] : _realtime.Notices;
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
    /// <summary>与同步数据库使用相同的服务、租户、已验证用户边界。</summary>
    private string NotificationAccount => _options.ApiBaseUrl.TrimEnd('/') + "\n" + _options.TenantId + "\n" + _session.SubjectId;
    public NotificationRule GetNotificationRule(string? projectId = null) => _notificationPreferences.Get(NotificationAccount, projectId);
    public bool HasNotificationOverride(string projectId) => _notificationPreferences.HasOverride(NotificationAccount, projectId);
    /// <summary>规则立即作用于等待合并和正在显示的提示，未读动态仍然完整保留。</summary>
    public void SetNotificationRule(string? projectId, NotificationRule? rule)
    {
        if (!_session.IsAuthenticated) return;
        _notificationPreferences.Set(NotificationAccount, projectId, rule);
        _pendingNotices.RemoveAll(x => !_notificationPreferences.Allows(NotificationAccount, x));
        _visibleNotices = _visibleNotices.Where(x => _notificationPreferences.Allows(NotificationAccount, x)).ToArray();
        StatusChanged?.Invoke();
    }
    private void NotifyCommitted(IReadOnlyList<SyncNotice> notices)
    {
        var identity = _session.IdentityVersion;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (identity != _session.IdentityVersion || !_session.IsAuthenticated) return;
            if (_notificationIdentity != identity) { ClearNotifications(); _notificationIdentity = identity; }
            _pendingNotices.AddRange(notices.Where(x => _notificationPreferences.Allows(NotificationAccount, x)));
            if (_pendingNotices.Count == 0) return;
            if (_notificationScheduled) return;
            _notificationScheduled = true;
            await Task.Delay(600);
            if (identity != _session.IdentityVersion) return;
            _notificationScheduled = false;
            // 合并期间已经点过已读的通知不再弹出；系统横幅消失本身不等于用户已读。
            var unread = RecentChanges.Select(x => x.Id).ToHashSet();
            var current = _pendingNotices.Where(x => unread.Contains(x.Id) && _notificationPreferences.Allows(NotificationAccount, x)).DistinctBy(x => x.Id).ToArray();
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
    /// <summary>后台通知与前台使用相同的完整变更内容，不替换为数量提示。</summary>
    private static string NativeNoticeText(SyncNotice[] notices) => string.Join("\n\n", notices.Select(x => x.DisplayMessage));
    private void ClearNotifications()
    {
        _notificationVersion++; _notificationScheduled = false; _pendingNotices.Clear();
        _visibleNotices = []; _notificationView = null;
    }
}
