namespace Asgard.Brigitte.Desktop.UI;

/// <summary>仅传递 BUG 与状态标识，落点不能携带身份或锁凭据。</summary>
public sealed record BugCardDrop(string BugId, string FromStatus, string ToStatus);
