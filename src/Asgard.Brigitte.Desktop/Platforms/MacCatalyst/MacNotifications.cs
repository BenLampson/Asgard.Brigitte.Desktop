using UserNotifications;

namespace Asgard.Brigitte.Desktop;

/// <summary>遵循 macOS 通知授权和免打扰设置，拒绝授权不影响同步。</summary>
internal static class MacNotifications
{
    internal static void Show(string title, string message)
    {
        var center = UNUserNotificationCenter.Current;
        void Deliver()
        {
            using var content = new UNMutableNotificationContent { Title = title, Body = message };
            using var request = UNNotificationRequest.FromIdentifier("brigitte-workspace", content, null);
            center.AddNotificationRequest(request, _ => { });
        }
        center.GetNotificationSettings(settings =>
        {
            if (settings.AuthorizationStatus == UNAuthorizationStatus.NotDetermined)
                center.RequestAuthorization(UNAuthorizationOptions.Alert, (allowed, _) => { if (allowed) Deliver(); });
            else if (settings.AuthorizationStatus == UNAuthorizationStatus.Authorized) Deliver();
        });
    }
}
