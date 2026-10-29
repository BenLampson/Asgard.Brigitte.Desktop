using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>自动领取、流转和释放的失败边界，全部使用专用临时 SQLite 和离线传输。</summary>
public sealed class BugStatusMoveTests
{
    [Fact]
    public async Task Unclaimed_card_acquires_changes_and_releases_one_unique_short_lived_lock()
    {
        using var rig = new BugStatusMoveTransport();
        var result = await rig.Move("pending_deployment", "测试环境 v2：修复完成");
        Assert.True(result.Changed); Assert.Null(result.Error); Assert.Null(result.ReleaseError);
        Assert.Equal(new[] { "claim", "status", "release-lock" }, rig.Requests.Select(r => r.Action));
        var owners = rig.Requests.Select(r => r.Body.GetProperty("workerId").GetString()).ToArray();
        Assert.Single(owners.Distinct()); Assert.StartsWith("board-move-", owners[0]);
        Assert.Equal(1, rig.Requests[0].Body.GetProperty("ttlMinutes").GetInt32());
        Assert.Equal("测试环境 v2：修复完成", rig.Requests[1].Body.GetProperty("reason").GetString());
        Assert.Equal("pending_deployment", rig.Server.Status); Assert.False(BugDisplay.HasLock(rig.Server));
    }

    [Fact]
    public async Task Rejected_claim_never_changes_or_releases_any_lock()
    {
        using var rig = new BugStatusMoveTransport { Reject = "claim" };
        var result = await rig.Move();
        Assert.False(result.Changed); Assert.NotNull(result.Error); Assert.Null(result.ReleaseError);
        Assert.Equal("claim", Assert.Single(rig.Requests).Action); Assert.Equal("processing", rig.Server.Status);
    }

    [Fact]
    public async Task Failed_status_still_releases_the_automatic_lock()
    {
        using var rig = new BugStatusMoveTransport { Reject = "status" };
        var result = await rig.Move();
        Assert.False(result.Changed); Assert.NotNull(result.Error); Assert.Null(result.ReleaseError);
        Assert.Equal(new[] { "claim", "status", "release-lock" }, rig.Requests.Select(r => r.Action));
        Assert.False(BugDisplay.HasLock(rig.Server)); Assert.Equal("processing", rig.Server.Status);
    }

    [Fact]
    public async Task Release_failure_reports_successful_status_separately()
    {
        using var rig = new BugStatusMoveTransport { Reject = "release-lock" };
        var result = await rig.Move();
        Assert.True(result.Changed); Assert.Null(result.Error); Assert.NotNull(result.ReleaseError);
        Assert.Equal("confirmed", rig.Server.Status);
        Assert.True(rig.Server.LockExpireTime <= DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Newer_status_returned_by_claim_is_not_overwritten()
    {
        using var rig = new BugStatusMoveTransport { StatusAtClaim = "blocked" };
        var result = await rig.Move();
        Assert.False(result.Changed); Assert.NotNull(result.Error); Assert.Null(result.ReleaseError);
        Assert.Equal(new[] { "claim", "release-lock" }, rig.Requests.Select(r => r.Action));
        Assert.Equal("blocked", rig.Server.Status); Assert.False(BugDisplay.HasLock(rig.Server));
    }

    [Theory]
    [InlineData("human_verified", "人工已验收")]
    [InlineData("closed", "归档")]
    [InlineData("pending_deployment", " ")]
    [InlineData("fixed", "历史")]
    public async Task Invalid_transitions_and_missing_reasons_do_not_claim(string status, string reason)
    {
        using var rig = new BugStatusMoveTransport();
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Move(status, reason));
        Assert.Empty(rig.Requests);
    }

    [Fact]
    public async Task Same_lane_and_stale_card_do_not_write()
    {
        using var rig = new BugStatusMoveTransport();
        Assert.False((await rig.Move("processing")).Changed);
        rig.Server.Status = "blocked"; rig.Publish();
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Move());
        Assert.Empty(rig.Requests);
    }

    [Theory]
    [InlineData("other-worker", "u")]
    [InlineData("desktop-worker", "another-user")]
    public async Task Existing_foreign_lock_prevents_all_commands(string worker, string user)
    {
        using var rig = new BugStatusMoveTransport();
        rig.Server.LockOwnerId = worker; rig.Server.LockOwnerUserId = user;
        rig.Server.LockExpireTime = DateTimeOffset.UtcNow.AddHours(1); rig.Publish();
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Move()); Assert.Empty(rig.Requests);
    }

    [Fact]
    public async Task Existing_manual_lock_is_preserved()
    {
        using var rig = new BugStatusMoveTransport();
        rig.Server.LockOwnerId = "desktop-worker"; rig.Server.LockOwnerUserId = "u";
        rig.Server.LockExpireTime = DateTimeOffset.UtcNow.AddHours(1); rig.Publish();
        var result = await rig.Move();
        Assert.True(result.Changed); Assert.Null(result.Error); Assert.Null(result.ReleaseError);
        Assert.Equal(new[] { "claim", "status" }, rig.Requests.Select(r => r.Action));
        Assert.Equal("desktop-worker", rig.Server.LockOwnerId);
        Assert.Equal(60, rig.Requests[0].Body.GetProperty("ttlMinutes").GetInt32());
    }

    [Fact]
    public async Task Manual_claim_also_rechecks_the_latest_status_without_releasing_it()
    {
        using var rig = new BugStatusMoveTransport { StatusAtClaim = "blocked" };
        rig.Server.LockOwnerId = "desktop-worker"; rig.Server.LockOwnerUserId = "u";
        rig.Server.LockExpireTime = DateTimeOffset.UtcNow.AddHours(1); rig.Publish();
        var result = await rig.Move();
        Assert.False(result.Changed); Assert.NotNull(result.Error); Assert.Null(result.ReleaseError);
        Assert.Equal("claim", Assert.Single(rig.Requests).Action);
        Assert.Equal("blocked", rig.Server.Status); Assert.Equal("desktop-worker", rig.Server.LockOwnerId);
    }

    [Fact]
    public async Task Account_change_cannot_send_status_or_cleanup_with_the_new_identity()
    {
        using var rig = new BugStatusMoveTransport();
        rig.AfterClaim = () => rig.Session.Version++;
        var result = await rig.Move();
        Assert.False(result.Changed); Assert.IsType<OperationCanceledException>(result.Error);
        Assert.IsType<OperationCanceledException>(result.ReleaseError);
        Assert.Equal("claim", Assert.Single(rig.Requests).Action);
    }

    [Fact]
    public async Task Failed_sync_after_claim_still_attempts_release_without_replaying_status()
    {
        using var rig = new BugStatusMoveTransport();
        rig.Sync.Barrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Sync.Barrier.SetException(new InvalidOperationException("sync unavailable"));
        var result = await rig.Move();
        Assert.False(result.Changed); Assert.NotNull(result.Error);
        Assert.Equal(new[] { "claim", "release-lock" }, rig.Requests.Select(r => r.Action));
        Assert.False(BugDisplay.HasLock(rig.Server));
    }

    [Fact]
    public async Task Local_card_moves_with_history_and_without_leaking_locks_or_using_HTTP()
    {
        using var local = new StandaloneTestWorkspace(); await local.EnterAsync();
        var project = await local.ProjectAsync();
        var bug = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "拖动测试" });
        var result = await local.Api.MoveStatusAsync(bug.Id, "waiting", "processing", "", "local-worker", StandaloneWorkspace.UserId);
        Assert.True(result.Changed); Assert.Null(result.Error); Assert.Null(result.ReleaseError);
        var detail = await local.Api.DetailAsync(bug.Id);
        Assert.Equal("processing", detail.Bug.Status); Assert.False(BugDisplay.HasLock(detail.Bug));
        var history = Assert.Single(detail.Histories); Assert.Equal("waiting", history.FromStatus); Assert.Equal("processing", history.ToStatus);
        Assert.Equal(0, local.Transport.Count);
        var claimed = await local.Api.ClaimAsync(bug.Id, "temporary-worker", 1);
        Assert.True(claimed.LockExpireTime <= DateTimeOffset.UtcNow.AddMinutes(1));
    }
}
