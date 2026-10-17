namespace Asgard.Brigitte.Desktop;

/// <summary>落库后的轻提示；不闪窗口、不移动焦点，前台仅当前窗口显示一次。</summary>
public sealed partial class DesktopHost
{
    private DesktopView? _activeView, _notificationView;
    private readonly List<SyncNotice> _pendingNotices = [];
    private int _notificationVersion;
    private long _notificationIdentity = -1;
    private bool _notificationScheduled;
    public string NotificationText { get; private set; } = "";
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
    public void DismissNotification() { NotificationText = ""; _notificationView = null; StatusChanged?.Invoke(); }
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
            NotificationText = string.Join("\n\n", current.Select(x => x.Message));
            _notificationView = _activeView?.Kind == DesktopViewKind.Tray ? null : _activeView;
            if (_activeView is null) _tray?.Notify("Brigitte · 工作区动态", NativeNoticeText(current));
            var version = ++_notificationVersion;
            RefreshNotices();
            await Task.Delay(8000);
            if (version == _notificationVersion) DismissNotification();
        });
    }
    /// <summary>Windows 气泡正文上限为 255 个 UTF-16 字符；完整动态始终留在未读列表。</summary>
    private static string NativeNoticeText(SyncNotice[] notices)
    {
        var suffix = notices.Length > 1 ? $"\n另有 {notices.Length - 1} 条动态，请查看托盘消息" : "";
        var first = notices[0].Message;
        var limit = 255 - suffix.Length;
        var lines = first.Split('\n', 2);
        if (lines.Length == 2)
        {
            // 项目和标题很长时也给操作人及变化留出空间，避免只看见 BUG 名称。
            var subject = TrimNativeNotice(lines[0], 110);
            first = subject + "\n" + TrimNativeNotice(lines[1], limit - subject.Length - 1);
        }
        else first = TrimNativeNotice(first, limit);
        return first + suffix;
    }
    /// <summary>按 Windows 原生容量截断，不切开 UTF-16 代理对。</summary>
    private static string TrimNativeNotice(string text, int limit)
    {
        if (text.Length <= limit) return text;
        var end = limit - 1;
        if (char.IsHighSurrogate(text[end - 1])) end--;
        return text[..end] + "…";
    }
    private void ClearNotifications()
    {
        _notificationVersion++; _notificationScheduled = false; _pendingNotices.Clear();
        NotificationText = ""; _notificationView = null;
    }
}
