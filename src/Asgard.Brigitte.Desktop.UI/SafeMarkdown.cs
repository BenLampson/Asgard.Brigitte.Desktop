using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
namespace Asgard.Brigitte.Desktop.UI;
/// <summary>渲染文本 Markdown 并保留普通换行；不接受 HTML、属性注入或活动 URL。</summary>
public static class SafeMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().UsePipeTables().UseTaskLists().UseAutoLinks().UseEmphasisExtras().UseSoftlineBreakAsHardlineBreak().Build();
    /// <summary>URL 校验发生在 Markdown 解码之后，阻止实体编码的协议绕过。</summary>
    public static string Render(string? value)
    {
        var document = Markdown.Parse(value ?? "", Pipeline);
        foreach (var link in document.Descendants<LinkInline>()) { if (!Allowed(link.Url)) { link.Url = ""; link.GetDynamicUrl = null; } }
        foreach (var link in document.Descendants<AutolinkInline>()) { if (!Allowed(link.Url)) link.Url = ""; }
        return document.ToHtml(Pipeline);
    }
    private static bool Allowed(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";
}
