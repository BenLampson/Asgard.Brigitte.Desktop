using System.Globalization;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>将当前项目的完整已同步数据导出为独立离线 HTML 报表。</summary>
public static class BugHtmlReport
{
    private static readonly string[] Colors = ["#4f6bed", "#168674", "#ba640d", "#cc4f65", "#7957bd", "#307da3", "#95643f", "#687b34", "#a65092", "#60728f"];
    private static readonly Lazy<string> Styles = new(() => ReadAsset("report.css"));
    private static readonly Lazy<string> Script = new(() => ReadAsset("report.js"));

    /// <summary>时间和每日分组都采用导出设备的本地时区，离线分享后保持一致。</summary>
    public static string Create(Project project, IReadOnlyList<Bug> bugs, IReadOnlyList<IdentityUser> users, DateTimeOffset generatedAt)
    {
        var rows = bugs.Where(x => x.ProjectId == project.Id)
            .OrderByDescending(x => x.CreateTime).ThenBy(x => x.Id, StringComparer.Ordinal)
            .Select(bug => new
            {
                Bug = bug,
                LocalTime = bug.CreateTime == default ? (DateTimeOffset?)null : bug.CreateTime.ToLocalTime(),
                Services = (bug.AffectedServices ?? []).Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Reporter = ReporterKey(bug.CreatedBy)
            }).ToArray();
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"").Append(ClientText.Language)
            .Append("\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>")
            .Append(T("BUG 报表")).Append("</title><style>").Append(Styles.Value).Append("</style></head><body><main><header>");
        html.Append("<h1>").Append(E(project.Name)).Append(" · ").Append(T("BUG 报表")).Append("</h1><p>")
            .Append(E(project.Code)).Append(" · ").Append(T("生成时间")).Append(' ');
        WriteTime(html, generatedAt.ToLocalTime(), "yyyy-MM-dd HH:mm:ss 'UTC'zzz");
        html.Append("</p></header><div class=\"metrics\">");
        Metric(html, "当前 BUG 总数", rows.Length);
        Metric(html, "已关闭", rows.Count(x => x.Bug.Status == "closed"));
        Metric(html, "待跟进", rows.Count(x => BugDisplay.IsUnresolved(x.Bug.Status)));
        html.Append("</div>");
        if (rows.Length > 0)
            html.Append("<div class=\"chart-hint\"><p class=\"muted\" id=\"chart-hint\" hidden>")
                .Append(T("点击图表或图例筛选明细；不同维度可叠加，再点一次取消。"))
                .Append("</p><a href=\"#bug-details\">").Append(T("查看 BUG 明细")).Append(" ↓</a></div>");
        if (rows.Any(x => x.LocalTime.HasValue)) WriteTrend(html);
        html.Append("<div class=\"charts\">");
        Chart(html, "状态分布", "status", Groups(rows.Select(x => x.Bug.Status), k => BugDisplay.Statuses.GetValueOrDefault(k, k)), ring: true);
        Chart(html, "优先级分布", "priority", Groups(rows.Select(x => x.Bug.Priority), k => BugDisplay.Priorities.GetValueOrDefault(k, k)), ring: true);
        Chart(html, "涉及服务 / 组件", "service",
            Groups(rows.SelectMany(x => x.Services.Length == 0 ? [""] : x.Services),
                k => k.Length == 0 ? ClientText.T("未标注") : AffectedServiceDisplay.Label(k), StringComparer.OrdinalIgnoreCase),
            note: "同一 BUG 可涉及多个组件，每个组件分别计数。");
        Chart(html, "按提出人统计", "reporter", Groups(rows.Select(x => x.Reporter), k => ReporterLabel(k, users)));
        html.Append("</div><section class=\"card details\" id=\"bug-details\"><h2>").Append(T("BUG 明细")).Append("</h2>");
        WriteControls(html);
        html.Append("<div class=\"table-scroll\"><table><thead><tr>");
        string[] columns = ["标题 / 编号", "状态", "优先级", "涉及服务 / 组件", "提出人", "负责人", "创建时间", "描述"];
        foreach (var title in columns) html.Append("<th scope=\"col\">").Append(T(title)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            var bug = row.Bug;
            var services = row.Services.Length == 0 ? [""] : row.Services.Select(s => s.ToLowerInvariant()).ToArray();
            html.Append("<tr class=\"bug-row\" data-status=\"").Append(E(bug.Status)).Append("\" data-priority=\"").Append(E(bug.Priority))
                .Append("\" data-service=\"").Append(E(JsonSerializer.Serialize(services)))
                .Append("\" data-reporter=\"").Append(E(row.Reporter)).Append("\" data-date=\"")
                .Append(row.LocalTime?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("\"><td>")
                .Append(E(bug.Title)).Append("<small>").Append(E(bug.Id)).Append("</small></td><td>")
                .Append(E(BugDisplay.Statuses.GetValueOrDefault(bug.Status, bug.Status))).Append("</td><td>")
                .Append(E(BugDisplay.Priorities.GetValueOrDefault(bug.Priority, bug.Priority))).Append("</td><td>")
                .Append(row.Services.Length == 0 ? T("未标注") : E(string.Join(" · ", row.Services.Select(AffectedServiceDisplay.Label))))
                .Append("</td><td>").Append(E(ReporterLabel(row.Reporter, users))).Append("</td><td>")
                .Append(E(BugDisplay.Assignee(bug.AssigneeId, users))).Append("</td><td>");
            if (row.LocalTime is { } time) WriteTime(html, time, "yyyy-MM-dd HH:mm");
            else html.Append('—');
            html.Append("</td><td class=\"description\">").Append(E(bug.Description)).Append("</td></tr>");
        }
        html.Append("<tr id=\"no-matches\"").Append(rows.Length > 0 ? " hidden" : "")
            .Append("><td colspan=\"").Append(columns.Length).Append("\">")
            .Append(T(rows.Length > 0 ? "没有符合筛选条件的 BUG" : "暂无 BUG 数据")).Append("</td></tr></tbody></table></div>");
        WritePagination(html);
        html.Append("</section><footer>").Append(T("此报表反映导出时当前项目的数据；已删除的 BUG 不计入统计。文件可离线打开。"))
            .Append(' ').Append(T("所有时间和每日统计均使用导出设备的本地时区。"))
            .Append("</footer></main><script>").Append(Script.Value).Append("</script></body></html>");
        return html.ToString();
    }

    /// <summary>旧 MCP 审计标识只表示来源，无法据此确认真人身份。</summary>
    private static string ReporterKey(string? value) => string.IsNullOrWhiteSpace(value) || value == "brigitte" ? ""
        : value.StartsWith("mcp:", StringComparison.Ordinal) ? "mcp:" : value;

    /// <summary>按已同步目录解析用户，目录外保留原始标识，缺失时明确显示未记录。</summary>
    private static string ReporterLabel(string key, IReadOnlyList<IdentityUser> users) => key switch
    {
        "" => ClientText.T("未记录"),
        "mcp:" => ClientText.T("MCP 操作者"),
        _ => BugDisplay.Assignee(key, users)
    };

    /// <summary>保留带偏移的机器可读值，同时用本地时间显示。</summary>
    private static void WriteTime(StringBuilder html, DateTimeOffset value, string format) =>
        html.Append("<time datetime=\"").Append(E(value.ToString("O", CultureInfo.InvariantCulture))).Append("\">")
            .Append(E(value.ToString(format, CultureInfo.InvariantCulture))).Append("</time>");

    /// <summary>输出项目总览卡片。</summary>
    private static void Metric(StringBuilder html, string name, int value) =>
        html.Append("<section class=\"card metric\"><span class=\"muted\">").Append(T(name)).Append("</span><strong>").Append(value).Append("</strong></section>");

    /// <summary>按原始键统计，避免翻译后的同名标签合并不同值。</summary>
    private static (string Key, string Label, int Count)[] Groups(IEnumerable<string> keys, Func<string, string> label, StringComparer? comparer = null) =>
        keys.GroupBy(x => x, comparer ?? StringComparer.Ordinal)
            .Select(g => (Key: comparer == StringComparer.OrdinalIgnoreCase ? g.Key.ToLowerInvariant() : g.Key, Label: label(g.Key), Count: g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label, StringComparer.Ordinal).ToArray();

    /// <summary>圆环和图例共享筛选键；多值组件与提出人使用横条便于比较。</summary>
    private static void Chart(StringBuilder html, string title, string dimension, (string Key, string Label, int Count)[] values, bool ring = false, string? note = null)
    {
        var total = values.Sum(x => x.Count);
        html.Append("<section class=\"card\"><h2>").Append(T(title)).Append("</h2>");
        if (note is not null) html.Append("<p class=\"muted chart-note\">").Append(T(note)).Append("</p>");
        html.Append("<div class=\"chart-body\">");
        if (ring)
        {
            html.Append("<svg class=\"donut\" viewBox=\"0 0 100 100\" aria-hidden=\"true\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"none\" stroke=\"#e9eef5\" stroke-width=\"15\"/>");
            var angle = -Math.PI / 2;
            for (var i = 0; i < values.Length; i++)
            {
                var (key, label, count) = values[i];
                var end = angle + 2 * Math.PI * count / total;
                if (values.Length == 1) html.Append("<circle cx=\"50\" cy=\"50\" r=\"40\"");
                else
                {
                    html.Append("<path d=\"M ").Append(Point(angle)).Append(" A 40 40 0 ")
                        .Append(end - angle > Math.PI ? '1' : '0').Append(" 1 ").Append(Point(end)).Append('"');
                }
                html.Append(" class=\"slice\" fill=\"none\" stroke-width=\"15\" stroke=\"").Append(Colors[i % Colors.Length]).Append('"');
                FilterAttributes(html, dimension, key, label);
                html.Append("><title>").Append(E(label)).Append(" · ").Append(count).Append("</title>")
                    .Append(values.Length == 1 ? "</circle>" : "</path>");
                angle = end;
            }
            html.Append("<text x=\"50\" y=\"51\" text-anchor=\"middle\" dominant-baseline=\"middle\">")
                .Append(total).Append("</text></svg>");
        }
        html.Append("<ul class=\"legend").Append(ring ? "" : " bars").Append("\">");
        for (var i = 0; i < values.Length; i++)
        {
            var (key, label, count) = values[i];
            html.Append("<li><button class=\"legend-item\" type=\"button\" aria-pressed=\"false\" disabled");
            FilterAttributes(html, dimension, key, label);
            html.Append("><span class=\"swatch\" style=\"background:").Append(Colors[(ring ? i : 0) % Colors.Length])
                .Append("\"></span><span>").Append(E(label)).Append("</span><b>").Append(count).Append("</b>");
            if (!ring)
                html.Append("<span class=\"bar-track\" aria-hidden=\"true\"><span style=\"width:")
                    .Append(F(100d * count / values[0].Count)).Append("%\"></span></span>");
            html.Append("</button></li>");
        }
        if (total == 0) html.Append("<li class=\"muted\">").Append(T("暂无数据")).Append("</li>");
        html.Append("</ul></div></section>");
    }

    /// <summary>输出趋势区域及独立的 30 天窗口导航。</summary>
    private static void WriteTrend(StringBuilder html) =>
        html.Append("<section class=\"card trend\" id=\"trend\" hidden data-point-label=\"").Append(T("提交量")).Append("\"><div class=\"section-head\"><div><h2>")
            .Append(T("每日 BUG 提交量")).Append("</h2><p class=\"muted chart-note\">").Append(T("按创建日期统计，点击日期查看当天明细。"))
            .Append("</p></div><div class=\"trend-nav\"><button id=\"older\" type=\"button\">").Append(T("前 30 天"))
            .Append("</button><span id=\"trend-range\" aria-live=\"polite\"></span><button id=\"newer\" type=\"button\">").Append(T("后 30 天"))
            .Append("</button></div></div><div class=\"trend-scroll\"><svg id=\"trend-plot\" viewBox=\"0 0 900 195\" role=\"group\" aria-label=\"")
            .Append(T("每日 BUG 提交量")).Append("\"></svg></div></section>");

    /// <summary>输出当前筛选及清除按钮。</summary>
    private static void WriteControls(StringBuilder html) =>
        html.Append("<div id=\"filter-bar\" class=\"filter-bar\" hidden><span class=\"muted\">").Append(T("当前筛选"))
            .Append("</span><div id=\"filters\"></div><button id=\"clear\" type=\"button\">").Append(T("清除全部筛选")).Append("</button></div>");

    /// <summary>输出明细页大小和翻页控件，禁用脚本时完整表格仍可读。</summary>
    private static void WritePagination(StringBuilder html) =>
        html.Append("<div id=\"pagination\" class=\"pagination\" hidden data-count=\"").Append(T("显示第 {0}–{1} 条，共 {2} 条")).Append("\" data-empty=\"")
            .Append(T("共 0 条")).Append("\"><span id=\"result-count\" role=\"status\"></span><div class=\"page-controls\"><label>")
            .Append(T("每页条数")).Append(" <select id=\"page-size\"><option>10</option><option selected>20</option><option>50</option><option>100</option></select></label>")
            .Append("<button id=\"prev\" type=\"button\">").Append(T("上一页")).Append("</button><span id=\"page-number\"></span><button id=\"next\" type=\"button\">")
            .Append(T("下一页")).Append("</button></div></div>");

    /// <summary>用户数据只进入转义后的属性，不拼入脚本。</summary>
    private static void FilterAttributes(StringBuilder html, string dimension, string key, string label) =>
        html.Append(" data-filter=\"").Append(dimension).Append("\" data-key=\"").Append(E(key)).Append("\" data-label=\"").Append(E(label)).Append('"');

    /// <summary>由角度计算圆环弧线的端点。</summary>
    private static string Point(double angle) => $"{F(50 + 40 * Math.Cos(angle))} {F(50 + 40 * Math.Sin(angle))}";

    /// <summary>报表资源内嵌于程序集，生成时写入同一个 HTML 文件。</summary>
    private static string ReadAsset(string name)
    {
        using var stream = typeof(BugHtmlReport).Assembly.GetManifestResourceStream($"Asgard.Brigitte.Desktop.Core.BugReport.{name}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string T(string value) => E(ClientText.T(value));
    private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
