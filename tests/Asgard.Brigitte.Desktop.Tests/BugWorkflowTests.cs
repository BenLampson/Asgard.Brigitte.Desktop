using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>历史状态、部署和人工验收边界不能因客户端升级而跳过。</summary>
public sealed class BugWorkflowTests
{
    [Theory]
    [InlineData("processing", "pending_deployment", true)]
    [InlineData("pending_deployment", "pending_regression", true)]
    [InlineData("pending_deployment", "human_verified", false)]
    [InlineData("pending_regression", "human_verified", true)]
    [InlineData("human_verified", "closed", true)]
    [InlineData("regressed", "closed", false)]
    [InlineData("regressed", "human_verified", false)]
    [InlineData("regressed", "pending_regression", true)]
    [InlineData("processing", "fixed", false)]
    [InlineData("processing", "regressed", false)]
    public void Transition_requires_deployment_and_human_acceptance(string from, string to, bool allowed)
        => Assert.Equal(allowed, BugWorkflow.CanTransition(from, to));

    [Theory]
    [InlineData("pending_deployment")]
    [InlineData("pending_regression")]
    [InlineData("human_verified")]
    [InlineData("fixed")]
    [InlineData("regressed")]
    public void Unarchived_results_still_need_follow_up(string status)
    {
        Assert.True(BugDisplay.IsUnresolved(status));
        Assert.False(BugDisplay.NeedsRepair(status));
    }

    [Fact]
    public void Verification_requires_a_written_result()
    {
        Assert.Throws<InvalidOperationException>(() => BugWorkflow.Validate("pending_regression", "human_verified", " "));
        BugWorkflow.Validate("pending_regression", "human_verified", "Production v2: manually checked and passed");
    }
}
