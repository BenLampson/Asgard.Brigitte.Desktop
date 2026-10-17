namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 PromptAction 数据契约。</summary>
public sealed class PromptAction
{
    /// <summary>接口字段 Key。</summary>
    public string Key { get; set; } = "";
    /// <summary>接口字段 Name。</summary>
    public string Name { get; set; } = "";
    /// <summary>接口字段 Action。</summary>
    public string Action { get; set; } = "";
    /// <summary>接口字段 ActionLabel。</summary>
    public string ActionLabel { get; set; } = "";
    /// <summary>接口字段 IsPrimary。</summary>
    public bool IsPrimary { get; set; }
    /// <summary>接口字段 SortOrder。</summary>
    public int SortOrder { get; set; }
}
