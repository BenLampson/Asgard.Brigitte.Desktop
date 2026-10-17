namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 Project 数据契约。</summary>
public sealed class Project
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>接口字段 Name。</summary>
    public string Name { get; set; } = "";
    /// <summary>接口字段 Code。</summary>
    public string Code { get; set; } = "";
    /// <summary>接口字段 Description。</summary>
    public string Description { get; set; } = "";
    /// <summary>接口字段 Status。</summary>
    public string Status { get; set; } = "";
    /// <summary>接口字段 OwnerId。</summary>
    public string? OwnerId { get; set; }
    /// <summary>接口字段 MemberUserIds。</summary>
    public string[] MemberUserIds { get; set; } = [];
    /// <summary>接口字段 CreateTime。</summary>
    public DateTimeOffset CreateTime { get; set; }
    /// <summary>接口字段 UpdateTime。</summary>
    public DateTimeOffset? UpdateTime { get; set; }
}
