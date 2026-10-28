namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本机 BUG 与全部关联数据的完整快照。</summary>
internal sealed record LocalBugArchiveSnapshot(Bug Bug, BugComment[] Comments, BugHistory[] Histories, BugAttachment[] Attachments);
