namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 BugHistory 数据契约。</summary>
public sealed class BugHistory
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>所属 BUG，由服务端同步。</summary>
    public string BugId { get; set; } = "";
    /// <summary>接口字段 FromStatus。</summary>
    public string? FromStatus { get; set; }
    /// <summary>接口字段 ToStatus。</summary>
    public string ToStatus { get; set; } = "";
    /// <summary>接口字段 Reason。</summary>
    public string? Reason { get; set; }
    /// <summary>接口字段 OperatorType。</summary>
    public string OperatorType { get; set; } = "";
    /// <summary>接口字段 CreateTime。</summary>
    public DateTimeOffset CreateTime { get; set; }
}
