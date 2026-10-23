using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>本机提示词明确离线边界，不能引导外部 AI 写入远程工作区。</summary>
public sealed class LocalPromptGuideTests
{
    [Fact]
    public async Task Local_single_and_batch_prompts_explain_manual_handoff()
    {
        using var language = ClientText.UseLanguage("zh-CN");
        using var local = new StandaloneTestWorkspace();
        await local.EnterAsync();
        var project = await local.ProjectAsync();
        var bug = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "需要修复" });
        foreach (var key in new[] { "localFixPrompt", "projectBatchFixPrompt" })
        {
            var prompt = await local.Api.PromptAsync(key, new() { ["bugId"] = bug.Id, ["projectId"] = project.Id });
            Assert.Contains("没有在线 MCP", prompt);
            Assert.Contains("不自动修改 SQLite", prompt);
            Assert.Contains("AI 自测不能代表人工通过", prompt);
            Assert.Contains(bug.Id, prompt);
        }
        await local.Api.StatusAsync(bug.Id, "pending_deployment", "修复已验证", null);
        var batch = await local.Api.PromptAsync("projectBatchFixPrompt", new() { ["projectId"] = project.Id });
        Assert.DoesNotContain(bug.Id, batch);
        Assert.Equal(0, local.Transport.Count);
    }
}
