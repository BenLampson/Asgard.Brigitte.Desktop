namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 BugInput 数据契约。</summary>
public sealed class BugInput
{
    /// <summary>接口字段 ProjectId。</summary>
    public string ProjectId { get; set; } = "";
    /// <summary>接口字段 Title。</summary>
    public string Title { get; set; } = "";
    /// <summary>接口字段 Description。</summary>
    public string Description { get; set; } = "";
    /// <summary>接口字段 Content。</summary>
    public string Content { get; set; } = "";
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
    /// <summary>接口字段 WorkerId。</summary>
    public string? WorkerId { get; set; }
}
