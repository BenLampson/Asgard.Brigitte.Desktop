namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 CreatedMcpKey 数据契约。</summary>
public sealed class CreatedMcpKey
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>接口字段 Name。</summary>
    public string Name { get; set; } = "";
    /// <summary>接口字段 PlainKey。</summary>
    public string PlainKey { get; set; } = "";
    /// <summary>接口字段 KeyPrefix。</summary>
    public string KeyPrefix { get; set; } = "";
}
