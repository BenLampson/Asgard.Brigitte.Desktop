namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 ProjectStatistics 数据契约。</summary>
public sealed class ProjectStatistics
{
    /// <summary>接口字段 ProjectId。</summary>
    public string ProjectId { get; set; } = "";
    /// <summary>接口字段 Total。</summary>
    public long Total { get; set; }
    /// <summary>接口字段 HighPriority。</summary>
    public long HighPriority { get; set; }
    /// <summary>本地同步数据中仍需处理的 BUG 数量。</summary>
    public long Unresolved { get; set; }
    /// <summary>未解决 BUG 中紧急和高优先级的数量。</summary>
    public long UnresolvedHighPriority { get; set; }
    /// <summary>接口字段 StatusCounts。</summary>
    public Dictionary<string, long> StatusCounts { get; set; } = [];
}
