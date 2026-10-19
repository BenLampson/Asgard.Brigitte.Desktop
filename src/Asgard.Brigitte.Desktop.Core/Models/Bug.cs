namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 Bug 数据契约。</summary>
public sealed class Bug
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>接口字段 ProjectId。</summary>
    public string ProjectId { get; set; } = "";
    /// <summary>接口字段 Title。</summary>
    public string Title { get; set; } = "";
    /// <summary>接口字段 Description。</summary>
    public string Description { get; set; } = "";
    /// <summary>接口字段 Content。</summary>
    public string Content { get; set; } = "";
    /// <summary>接口字段 Status。</summary>
    public string Status { get; set; } = "";
    /// <summary>接口字段 Priority。</summary>
    public string Priority { get; set; } = "";
    /// <summary>接口字段 AssigneeId。</summary>
    public string? AssigneeId { get; set; }
    /// <summary>接口字段 AffectedServices。</summary>
    public string[] AffectedServices { get; set; } = [];
    /// <summary>接口字段 DeploymentNotes。</summary>
    public string DeploymentNotes { get; set; } = "";
    /// <summary>接口字段 Source。</summary>
    public string Source { get; set; } = "";
    /// <summary>接口字段 SourceRef。</summary>
    public string? SourceRef { get; set; }
    /// <summary>接口字段 LockOwnerId。</summary>
    public string? LockOwnerId { get; set; }
    /// <summary>服务端验证的领取用户；旧记录缺少该字段时不能根据 Worker 猜测用户。</summary>
    public string? LockOwnerUserId { get; set; }
    /// <summary>接口字段 LockOwnerName。</summary>
    public string? LockOwnerName { get; set; }
    /// <summary>接口字段 LockTime。</summary>
    public DateTimeOffset? LockTime { get; set; }
    /// <summary>接口字段 LockExpireTime。</summary>
    public DateTimeOffset? LockExpireTime { get; set; }
    /// <summary>接口字段 CreateTime。</summary>
    public DateTimeOffset CreateTime { get; set; }
    /// <summary>接口字段 UpdateTime。</summary>
    public DateTimeOffset? UpdateTime { get; set; }
}
