namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 BugAttachment 数据契约。</summary>
public sealed class BugAttachment
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>所属 BUG，由服务端同步。</summary>
    public string BugId { get; set; } = "";
    /// <summary>接口字段 FileName。</summary>
    public string FileName { get; set; } = "";
    /// <summary>接口字段 ContentType。</summary>
    public string ContentType { get; set; } = "";
    /// <summary>接口字段 Base64Content。</summary>
    public string Base64Content { get; set; } = "";
    /// <summary>接口字段 SizeBytes。</summary>
    public long SizeBytes { get; set; }
    /// <summary>接口字段 CreateTime。</summary>
    public DateTimeOffset CreateTime { get; set; }
}
