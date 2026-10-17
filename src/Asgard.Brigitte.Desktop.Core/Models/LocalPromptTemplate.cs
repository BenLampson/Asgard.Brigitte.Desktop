namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>从服务端同步的提示词及按钮规则，渲染和动作查询均在本地完成。</summary>
public sealed class LocalPromptTemplate
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "";
    public string Action { get; set; } = "";
    public string ActionLabel { get; set; } = "";
    public string[] ApplicableStatuses { get; set; } = [];
    public string[] PrimaryStatuses { get; set; } = [];
    public int SortOrder { get; set; }
    public bool Enabled { get; set; }
}
