namespace Asgard.Brigitte.Desktop.Core;

/// <summary>将已知服务标识转换为界面名称，自定义服务保持原样。</summary>
public static class AffectedServiceDisplay
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["frontend"] = "前端",
        ["backend"] = "后端",
        ["database"] = "数据库",
        ["nginx"] = "Nginx",
        ["configuration"] = "配置",
        ["scheduled-job"] = "定时任务",
        ["message-queue"] = "消息队列"
    };

    /// <summary>只匹配完整的内置标识，不改写服务名称或保存的数据。</summary>
    public static string Label(string value) => Labels.TryGetValue(value, out var label) ? ClientText.T(label) : value;
}
