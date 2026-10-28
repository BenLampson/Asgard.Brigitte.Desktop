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
            new() { Id = "1", ProjectId = "p", Status = "closed", Priority = "high", Title = "<img src=x onerror=alert(1)>" },
            new() { Id = "2", ProjectId = "p", Status = "waiting", Priority = "low", Title = "普通 BUG" },
            new() { Id = "3", ProjectId = "foreign", Status = "closed", Title = "不应出现" }
        ];
        var html = BugHtmlReport.Create(project, bugs, [], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Contains("状态分布", html); Assert.Contains("优先级分布", html); Assert.Contains("<svg", html);
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("&lt;img", html);
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("<img", html); Assert.DoesNotContain("不应出现", html);
        Assert.Contains("已删除的 BUG 不计入统计", html);
    }

    [Fact]
    public void Report_respects_the_selected_language()
    {
        using var language = ClientText.UseLanguage("en");
        var html = BugHtmlReport.Create(new Project { Id = "p", Name = "Project" }, [], [], DateTimeOffset.UnixEpoch);
        Assert.Contains("lang=\"en\"", html); Assert.Contains("Status breakdown", html);
        Assert.Contains("No bugs", html); Assert.DoesNotContain("状态分布", html);
    }

    [Fact]
    public async Task Report_preview_can_be_rendered_from_sample_data()
    {
        if (Environment.GetEnvironmentVariable("BRIGITTE_VALIDATE_LAYOUT") != "1") return;
        using var language = ClientText.UseLanguage("zh-CN");
        var project = new Project { Id = "sample", Name = "示例预算项目", Code = "BUDGET" };
        var statuses = new[] { "waiting", "processing", "pending_regression", "closed", "closed", "closed" };
        var bugs = statuses.Select((status, i) => new Bug { Id = "BUG-" + (1000 + i), ProjectId = project.Id, Title = "示例问题 " + (i + 1),
            Description = "用于验证报表布局的测试数据。", Status = status, Priority = i % 2 == 0 ? "high" : "medium", CreateTime = DateTimeOffset.UtcNow }).ToArray();
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var output = Path.Combine(root, "tests/Asgard.Brigitte.Desktop.Tests/obj/ui-validation"); Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "bug-report.html"), BugHtmlReport.Create(project, bugs, [], DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
    }
}
