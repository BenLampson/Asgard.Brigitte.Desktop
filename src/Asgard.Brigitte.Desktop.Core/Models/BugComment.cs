namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 BugComment 数据契约。</summary>
public sealed class BugComment
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>所属 BUG，由服务端同步。</summary>
    public string BugId { get; set; } = "";
    /// <summary>接口字段 Content。</summary>
    public string Content { get; set; } = "";
    /// <summary>接口字段 CommentType。</summary>
    public string CommentType { get; set; } = "";
    /// <summary>接口字段 AuthorType。</summary>
    public string AuthorType { get; set; } = "";
    /// <summary>接口字段 CreateTime。</summary>
    public DateTimeOffset CreateTime { get; set; }
}
