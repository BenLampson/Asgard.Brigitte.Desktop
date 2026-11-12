using System.Globalization;
using System.Net;
using System.Text.Json;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>报表离线显示当前项目，且不执行 BUG 内容。</summary>
public sealed class BugHtmlReportTests
{
    [Fact]
    public void Report_contains_project_statistics_and_escapes_untrusted_text()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        var project = new Project { Id = "p", Name = "<script>alert(1)</script>", Code = "TEST" };
        Bug[] bugs = [
            new() { Id = "1", ProjectId = "p", Status = "closed", Priority = "high", Title = "<img src=x onerror=alert(1)>",
                AffectedServices = ["</script><img src=x onerror=alert(1)>"], CreatedBy = "\"><img src=x onerror=alert(1)>" },
            new() { Id = "2", ProjectId = "p", Status = "waiting", Priority = "low", Title = "普通 BUG" },
            new() { Id = "3", ProjectId = "foreign", Status = "closed", Title = "不应出现" }
        ];
        var html = BugHtmlReport.Create(project, bugs, [], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Contains("状态分布", html); Assert.Contains("优先级分布", html); Assert.Contains("<svg", html);
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("&lt;img", html);
        Assert.Equal(2, html.Split("<script>", StringSplitOptions.None).Length);
        Assert.Equal(2, html.Split("</script>", StringSplitOptions.None).Length);
        Assert.DoesNotContain("<img", html); Assert.DoesNotContain("不应出现", html);
        Assert.Contains("已删除的 BUG 不计入统计", html);
    }

    [Fact]
    public void Report_uses_synced_reporter_and_components_and_groups_by_the_displayed_local_day()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        var instant = new DateTimeOffset(2026, 11, 11, 18, 18, 0, TimeSpan.Zero);
        var local = instant.ToLocalTime();
        var bug = JsonSerializer.Deserialize<Bug>("""
            {"id":"b1","projectId":"p","createdBy":"creator","assigneeId":"owner","affectedServices":["frontend","backend","deployment"]}
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        bug.CreateTime = instant;
        IdentityUser[] users = [new() { Id = "creator", DisplayName = "提出者甲" }, new() { Id = "owner", DisplayName = "负责人乙" }];
        var html = BugHtmlReport.Create(new Project { Id = "p" }, [bug], users, instant);
        Assert.Contains("前端 · 后端 · deployment</td><td>提出者甲</td><td>负责人乙", WebUtility.HtmlDecode(html));
        Assert.Contains("data-date=\"" + local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\"", html);
        Assert.Contains(">" + local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "</time>", html);
        Assert.Contains(">" + local.ToString("yyyy-MM-dd HH:mm:ss 'UTC'zzz", CultureInfo.InvariantCulture) + "</time>", html);
    }

    [Fact]
    public void Report_respects_the_selected_language()
    {
        using var language = ClientText.UseLanguage("en");
        var html = BugHtmlReport.Create(new Project { Id = "p", Name = "Project" }, [], [], DateTimeOffset.UnixEpoch);
        Assert.Contains("lang=\"en\"", html); Assert.Contains("Status breakdown", html);
        Assert.Contains("No bugs", html); Assert.Contains("Bugs by reporter", html); Assert.DoesNotContain("状态分布", html);
    }

    [Fact]
    public async Task Report_preview_can_be_rendered_from_sample_data()
    {
        if (Environment.GetEnvironmentVariable("BRIGITTE_VALIDATE_LAYOUT") != "1") return;
        using var language = ClientText.UseLanguage("zh-CN");
        var project = new Project { Id = "sample", Name = "示例预算项目", Code = "BUDGET" };
        string[] statuses = ["waiting", "processing", "pending_regression", "closed", "closed", "closed"];
        var latest = new DateTimeOffset(2026, 11, 11, 18, 18, 0, TimeSpan.Zero);
        var bugs = Enumerable.Range(0, 66).Select(i => new Bug
        {
            Id = $"BUG-{1000 + i}", ProjectId = project.Id, Title = $"示例问题 {i + 1}",
            Description = "用于验证报表布局和联动的测试数据。", Status = statuses[i % statuses.Length],
            Priority = i % 3 == 0 ? "high" : "medium",
            AffectedServices = (i % 4) switch { 0 => ["frontend", "backend", "Frontend"], 1 => ["backend"], 2 => ["deployment"], _ => [] },
            AssigneeId = i % 2 == 0 ? "user-a" : null,
            CreatedBy = (i % 3) switch { 0 => "user-b", 1 => "user-a", _ => null },
            CreateTime = latest.AddDays(-2 * (i / 3))
        }).ToArray();
        IdentityUser[] users = [new() { Id = "user-a", DisplayName = "林小雨" }, new() { Id = "user-b", DisplayName = "陈一鸣" }];
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var output = Path.Combine(root, "tests/Asgard.Brigitte.Desktop.Tests/obj/ui-validation"); Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "bug-report.html"), BugHtmlReport.Create(project, bugs, users, latest.AddHours(8)), TestContext.Current.CancellationToken);
    }
}
