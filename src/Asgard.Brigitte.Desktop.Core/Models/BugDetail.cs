namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 BugDetail 数据契约。</summary>
public sealed class BugDetail
{
    /// <summary>接口字段 Bug。</summary>
    public Bug Bug { get; set; } = new();
    /// <summary>接口字段 Comments。</summary>
    public BugComment[] Comments { get; set; } = [];
    /// <summary>接口字段 Histories。</summary>
    public BugHistory[] Histories { get; set; } = [];
    /// <summary>接口字段 Attachments。</summary>
    public BugAttachment[] Attachments { get; set; } = [];
}
