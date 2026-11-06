using System.Text.Json.Serialization;

namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>团队空间的在线附件容量。</summary>
public sealed class ProjectStorage
{
    /// <summary>当前使用的附件字节数。</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long UsedBytes { get; set; }
    /// <summary>空间附件配额。</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long QuotaBytes { get; set; }
    /// <summary>套餐名称。</summary>
    public string PlanName { get; set; } = "";
}
