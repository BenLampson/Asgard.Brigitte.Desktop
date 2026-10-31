using System.Text;
using System.Text.Json;

namespace Asgard.Brigitte.Sync;

/// <summary>一条处理记录的结构化内容；空分项不要求填充。</summary>
public sealed class BugProcessReport
{
    /// <summary>结构版本，无法识别时客户端使用兼容正文。</summary>
    public int Version { get; set; } = 1;
    /// <summary>历史回填保留原始正文供用户核对，不代表该正文由结构重新生成。</summary>
    public bool IsLegacyImport { get; set; }
    /// <summary>面向提交人的处理摘要。</summary>
    public string Summary { get; set; } = "";
    /// <summary>根因与修改，可使用 Markdown。</summary>
    public string? Changes { get; set; }
    /// <summary>验证过程及实际结果，可使用 Markdown。</summary>
    public string? Verification { get; set; }
    /// <summary>提交与部署信息，可使用 Markdown。</summary>
    public string? Delivery { get; set; }
    /// <summary>未完成事项和后续动作，可使用 Markdown。</summary>
    public string? FollowUp { get; set; }
    /// <summary>不适合其他分项的补充信息，可使用 Markdown。</summary>
    public string? Other { get; set; }

    /// <summary>校验结构化输入，同时约束本机及在线模式的正文大小。</summary>
    public void Validate()
    {
        if (Version != 1) throw new ArgumentException("不支持的处理记录版本。", nameof(Version));
        if (string.IsNullOrWhiteSpace(Summary)) throw new ArgumentException("请填写处理摘要。", nameof(Summary));
        if (ToLegacyMarkdown().Length > 1_000_000) throw new ArgumentException("处理记录内容过长。");
    }

    /// <summary>生成供旧客户端和既有备份读取的完整 Markdown，结构数据仍是新记录的权威来源。</summary>
    public string ToLegacyMarkdown()
    {
        var text = new StringBuilder("## 处理摘要\n\n").Append(Summary);
        var sections = new (string Title, string? Content)[]
        {
            ("根因与修改", Changes), ("验证情况", Verification), ("提交与部署", Delivery),
            ("未完成事项", FollowUp), ("其他说明", Other)
        };
        if (sections.Any(section => !string.IsNullOrWhiteSpace(section.Content)))
        {
            text.Append("\n\n## 交接记录");
            foreach (var (title, content) in sections)
                if (!string.IsNullOrWhiteSpace(content)) text.Append("\n\n### ").Append(title).Append("\n\n").Append(content);
        }
        return text.ToString();
    }

    /// <summary>读取已存结构；旧、损坏或未来版本都保留兼容正文展示。</summary>
    public static BugProcessReport? FromJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var report = JsonSerializer.Deserialize<BugProcessReport>(value, SyncProtocol.Json);
            return report is { Version: 1 } && !string.IsNullOrWhiteSpace(report.Summary) ? report : null;
        }
        catch (JsonException) { return null; }
    }
}
