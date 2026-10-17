namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 McpKey 数据契约。</summary>
public sealed class McpKey
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>接口字段 Name。</summary>
    public string Name { get; set; } = "";
    /// <summary>接口字段 KeyPrefix。</summary>
    public string KeyPrefix { get; set; } = "";
    /// <summary>接口字段 Scopes。</summary>
    public string[] Scopes { get; set; } = [];
    /// <summary>接口字段 Enabled。</summary>
    public bool Enabled { get; set; }
    /// <summary>接口字段 CreateTime。</summary>
    public DateTimeOffset CreateTime { get; set; }
    /// <summary>接口字段 UpdateTime。</summary>
    public DateTimeOffset? UpdateTime { get; set; }
}
