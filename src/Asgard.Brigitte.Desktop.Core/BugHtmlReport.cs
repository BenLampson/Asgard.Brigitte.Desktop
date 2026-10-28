using System.Globalization;
using System.Net;
using System.Text;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>将当前项目的完整已同步数据导出为独立离线 HTML 报表。</summary>
public static class BugHtmlReport
{
    private static readonly string[] Colors = ["#4f6bed", "#12a890", "#e69b33", "#dd6270", "#8975c5", "#508dab", "#b98955", "#8a9a54", "#ba75a3", "#707d94", "#5c8596", "#a67262", "#9d9047", "#6f987d", "#775e94", "#b37270", "#5982be", "#8c867d"];

    public static string Create(Project project, IReadOnlyList<Bug> bugs, IReadOnlyList<IdentityUser> users, DateTimeOffset generatedAt)
    {
        var rows = bugs.Where(x => x.ProjectId == project.Id).OrderByDescending(x => x.CreateTime).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"").Append(ClientText.Language == "en" ? "en" : "zh-CN")
            .Append("\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>")
            .Append(T("BUG 报表")).Append("</title>");
        html.Append("""
            <style>
            *{box-sizing:border-box}body{margin:0;background:#f3f6fb;color:#17233a;font:15px/1.6 system-ui,-apple-system,sans-serif}main{width:90%;margin:0 auto;padding:40px 0}header{background:#172c50;color:white;border-radius:18px;padding:32px}h1,h2,p{margin:0}header p{color:#cfddf6;margin-top:8px}.metrics,.charts{display:grid;gap:16px;margin-top:20px;grid-template-columns:repeat(3,minmax(0,1fr))}.charts{grid-template-columns:repeat(2,minmax(0,1fr))}.card{background:white;border:1px solid #e3e9f2;border-radius:16px;padding:22px}.metric strong{display:block;font-size:32px}.muted{color:#6b7890}.chart-body{display:flex;align-items:center;gap:24px;margin-top:16px}.chart-body svg{width:150px;flex:none}.legend{padding:0;list-style:none;margin:0;flex:1;min-width:0}.legend li{display:flex;align-items:center;gap:8px;margin:5px 0}.legend i{width:10px;height:10px;border-radius:50%;flex:none}.legend b{margin-left:auto}.table{overflow:auto;margin-top:20px}table{border-collapse:collapse;width:100%;min-width:760px}th,td{text-align:left;vertical-align:top;border-bottom:1px solid #e9eef5;padding:10px 8px}th{font-size:13px;color:#6b7890;white-space:nowrap}td small{color:#6b7890;display:block;word-break:break-all}.description{white-space:pre-wrap;overflow-wrap:anywhere}footer{margin-top:20px;color:#6b7890;font-size:13px}@media(max-width:760px){.metrics,.charts{grid-template-columns:1fr}.chart-body{flex-wrap:wrap}}@media print{body{background:white}main{padding:0}.card,header{break-inside:avoid}}
            </style></head><body><main><header>
            """);
        html.Append("<h1>").Append(E(project.Name)).Append(" · ").Append(T("BUG 报表")).Append("</h1><p>").Append(E(project.Code)).Append(" · ").Append(T("生成时间")).Append(' ')
            .Append(E(generatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture))).Append("</p></header>");
        html.Append("<div class=\"metrics\">");
        Metric(html, "当前 BUG 总数", rows.Length);
        Metric(html, "已关闭", rows.Count(x => x.Status == "closed"));
        Metric(html, "待跟进", rows.Count(x => BugDisplay.IsUnresolved(x.Status)));
        html.Append("</div><div class=\"charts\">");
        Chart(html, "状态分布", rows.GroupBy(x => x.Status).Select(g => (BugDisplay.Statuses.GetValueOrDefault(g.Key, g.Key), g.Count())).ToArray());
        Chart(html, "优先级分布", rows.GroupBy(x => x.Priority).Select(g => (BugDisplay.Priorities.GetValueOrDefault(g.Key, g.Key), g.Count())).ToArray());
        html.Append("</div><section class=\"card table\"><h2>").Append(T("BUG 明细")).Append("</h2><table><thead><tr>");
        foreach (var title in new[] { "标题 / 编号", "状态", "优先级", "负责人", "创建时间", "描述" })
            html.Append("<th>").Append(T(title)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var bug in rows)
        {
            html.Append("<tr><td>").Append(E(bug.Title)).Append("<small>").Append(E(bug.Id)).Append("</small></td><td>")
                .Append(E(BugDisplay.Statuses.GetValueOrDefault(bug.Status, bug.Status))).Append("</td><td>")
                .Append(E(BugDisplay.Priorities.GetValueOrDefault(bug.Priority, bug.Priority))).Append("</td><td>")
                .Append(E(BugDisplay.Assignee(bug.AssigneeId, users))).Append("</td><td>")
                .Append(E(bug.CreateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)))
                .Append("</td><td class=\"description\">").Append(E(bug.Description)).Append("</td></tr>");
        }
        if (rows.Length == 0) html.Append("<tr><td colspan=\"6\">").Append(T("暂无 BUG 数据")).Append("</td></tr>");
        html.Append("</tbody></table></section><footer>").Append(T("此报表反映导出时当前项目的数据；已删除的 BUG 不计入统计。文件可离线打开。"))
            .Append("</footer></main></body></html>");
        return html.ToString();
    }

    private static void Metric(StringBuilder html, string name, int value) =>
        html.Append("<section class=\"card metric\"><span class=\"muted\">").Append(T(name)).Append("</span><strong>").Append(value).Append("</strong></section>");

    private static void Chart(StringBuilder html, string title, (string Label, int Count)[] groups)
    {
        var values = groups.OrderByDescending(x => x.Count).ThenBy(x => x.Label, StringComparer.Ordinal).ToArray();
        var total = values.Sum(x => x.Count);
        html.Append("<section class=\"card\"><h2>").Append(T(title)).Append("</h2><div class=\"chart-body\"><svg viewBox=\"0 0 100 100\" role=\"img\" aria-label=\"").Append(T(title)).Append("\"><circle cx=\"50\" cy=\"50\" r=\"42\" fill=\"none\" stroke=\"#e9eef5\" stroke-width=\"16\"/>");
        var offset = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            var percent = 100d * values[i].Count / total;
            html.Append("<circle cx=\"50\" cy=\"50\" r=\"42\" fill=\"none\" stroke=\"").Append(Colors[i % Colors.Length])
                .Append("\" stroke-width=\"16\" pathLength=\"100\" stroke-dasharray=\"").Append(F(percent)).Append(' ').Append(F(100 - percent))
                .Append("\" stroke-dashoffset=\"").Append(F(-offset)).Append("\" transform=\"rotate(-90 50 50)\"/>");
            offset += percent;
        }
        html.Append("<text x=\"50\" y=\"54\" text-anchor=\"middle\" font-size=\"16\" fill=\"#17233a\">").Append(total).Append("</text></svg><ul class=\"legend\">");
        for (var i = 0; i < values.Length; i++)
            html.Append("<li><i style=\"background:").Append(Colors[i % Colors.Length]).Append("\"></i><span>").Append(E(values[i].Label)).Append("</span><b>").Append(values[i].Count).Append("</b></li>");
        if (total == 0) html.Append("<li>").Append(T("暂无数据")).Append("</li>");
        html.Append("</ul></div></section>");
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string T(string value) => E(ClientText.T(value));
    private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
