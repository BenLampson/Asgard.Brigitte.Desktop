namespace Asgard.Brigitte.Desktop.Core;

/// <summary>提醒筛选使用结构化类型，不依赖通知正文或界面语言。</summary>
[Flags]
public enum NotificationChange
{
    None = 0,
    Created = 1,
    Status = 2,
    Assignment = 4,
    Details = 8,
    Comments = 16,
    Attachments = 32,
    Project = 64,
    Removed = 128,
    All = Created | Status | Assignment | Details | Comments | Attachments | Project | Removed
}
