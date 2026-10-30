using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>历史和人工验收保持原有语义；人工直接关闭需要填写原因。</summary>
public sealed class BugWorkflowTests
{
    [Theory]
    [InlineData("processing", "pending_deployment", true)]
    [InlineData("pending_deployment", "pending_regression", true)]
    [InlineData("pending_deployment", "human_verified", false)]
    [InlineData("pending_regression", "human_verified", true)]
    [InlineData("human_verified", "closed", true)]
    [InlineData("regressed", "closed", true)]
    [InlineData("regressed", "human_verified", false)]
    [InlineData("regressed", "pending_regression", true)]
    [InlineData("processing", "fixed", false)]
    [InlineData("processing", "regressed", false)]
    public void Deployment_and_human_acceptance_keep_their_prerequisites(string from, string to, bool allowed)
        => Assert.Equal(allowed, BugWorkflow.CanTransition(from, to));

    /// <summary>所有现有状态都能直接关闭，但状态真正变化时必须填写关闭原因。</summary>
    [Fact]
    public void Every_status_can_close_without_claiming_human_verification()
    {
        foreach (var status in BugDisplay.BoardStatuses)
        {
            Assert.True(BugWorkflow.CanTransition(status, "closed"));
            BugWorkflow.Validate(status, "closed", "人工决定直接关闭");
            if (status != "closed")
                Assert.Throws<InvalidOperationException>(() => BugWorkflow.Validate(status, "closed", " "));
        }
    }

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
