using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证用户看到的数据只来自已提交的线上同步，覆盖中断、校验失败和账号切换。</summary>
public sealed class WorkspaceSyncTests
{
    private static SyncRow Bug(string title) => SyncProtocol.Row("bug", "b", new Bug { Id = "b", ProjectId = "p", Title = title });
    /// <summary>托盘只统计未解决事项，已处理的高优先级 BUG 不再制造待办数量。</summary>
    [Fact]
    public void Unresolved_statistics_exclude_completed_states_and_change_only_after_commit()
    {
        using var fixture = new SyncTestWorkspace();
        var rows = BugDisplay.Statuses.Keys.Select(status => SyncProtocol.Row("bug", status,
            new Bug { Id = status, ProjectId = "p", Status = status, Priority = status == "reopened" ? "critical" : "high" })).ToList();
        rows.Add(SyncProtocol.Row("project", "p", new Project { Id = "p" }));
        rows.Add(SyncProtocol.Row("project", "empty", new Project { Id = "empty" }));
        rows.Add(SyncProtocol.Row("bug", "low", new Bug { Id = "low", ProjectId = "p", Status = "waiting", Priority = "low" }));
        fixture.Replace(rows.ToArray());
        var reader = new LocalWorkspaceReader(fixture.Database);
        var stats = reader.Statistics().Single(x => x.ProjectId == "p");
        Assert.Equal(16, stats.Total); Assert.Equal(10, stats.Unresolved); Assert.Equal(9, stats.UnresolvedHighPriority);
        var empty = reader.Statistics().Single(x => x.ProjectId == "empty");
        Assert.Equal(0, empty.Unresolved); Assert.Equal(0, empty.UnresolvedHighPriority);
        rows.RemoveAll(x => x.Key == "bug/reopened");
        var resolved = SyncProtocol.Row("bug", "reopened", new Bug { Id = "reopened", ProjectId = "p", Status = "fixed", Priority = "critical" });
        rows.Add(resolved);
        var frame = fixture.Begin(rows.ToArray()); fixture.Database.Apply(frame with { Phase = "rows", Rows = [resolved] });
        Assert.Equal(9, reader.Statistics().Single(x => x.ProjectId == "p").UnresolvedHighPriority);
        fixture.Database.Apply(frame with { Phase = "commit" });
        stats = reader.Statistics().Single(x => x.ProjectId == "p");
        Assert.Equal(9, stats.Unresolved); Assert.Equal(8, stats.UnresolvedHighPriority);
    }
    [Fact]
    public void Notifications_are_committed_once_and_replay_and_lock_renewals_are_silent()
    {
        using var fixture = new SyncTestWorkspace(); var db = fixture.Database;
        fixture.Replace(Bug("first")); Assert.Empty(db.Notices());
        fixture.Replace(Bug("second")); Assert.Single(db.Notices()); Assert.False(db.Notices()[0].Read);
        fixture.Replace(Bug("second")); Assert.Single(db.Notices());
        var renewal = SyncProtocol.Row("bug", "b", new Bug { Id = "b", ProjectId = "p", Title = "second", LockTime = DateTimeOffset.UtcNow, LockExpireTime = DateTimeOffset.UtcNow.AddHours(1), UpdateTime = DateTimeOffset.UtcNow });
        fixture.Replace(renewal); Assert.Single(db.Notices());
        db.MarkNoticesRead(); Assert.Empty(db.Notices());
        db.Close(); db.OpenAccount("test"); fixture.Replace(Bug("missed while offline"));
        Assert.Empty(db.Notices());
    }
    [Fact]
    public void Partial_batch_is_invisible_and_interruption_keeps_previous_commit()
    {
        using var fixture = new SyncTestWorkspace(); var db = fixture.Database;
        fixture.Replace(Bug("old")); var notifications = 0; db.Committed += _ => notifications++;
        var frame = fixture.Begin(Bug("new")); db.Apply(frame with { Phase = "rows", Rows = [Bug("new")] });
        Assert.Equal("old", Assert.Single(db.Read<Bug>("bug")).Title); Assert.Equal(0, notifications);
        db.AbortBatch(); Assert.Equal("old", Assert.Single(db.Read<Bug>("bug")).Title);
        fixture.Replace(Bug("new")); Assert.Equal("new", Assert.Single(db.Read<Bug>("bug")).Title); Assert.Equal(1, notifications);
    }
    [Fact]
    public void Invalid_checksum_or_missing_rows_cannot_advance_cursor()
    {
        using var fixture = new SyncTestWorkspace(); var db = fixture.Database; fixture.Replace(Bug("old"));
        var cursor = SyncProtocol.Cursor(db.Manifest());
        var frame = fixture.Begin(Bug("new"));
        Assert.Throws<InvalidDataException>(() => db.Apply(frame with { Phase = "commit" }));
        db.Apply(frame with { Phase = "rows", Rows = [Bug("different")] });
        Assert.Throws<InvalidDataException>(() => db.Apply(frame with { Phase = "commit" }));
        Assert.Equal(cursor, SyncProtocol.Cursor(db.Manifest())); Assert.Equal("old", Assert.Single(db.Read<Bug>("bug")).Title);
    }
    [Fact]
    public void Reconnect_is_incremental_deletes_are_atomic_and_restart_requires_validation()
    {
        using var fixture = new SyncTestWorkspace(); var db = fixture.Database;
        fixture.Replace(Bug("old"), SyncProtocol.Row("comment", "c", new BugComment { Id = "c", BugId = "b", Content = "record" }));
        var previous = db.Manifest();
        Assert.Empty(SyncProtocol.Difference(previous.ToDictionary(x => x.Key, x => x.Hash), [Bug("old"), SyncProtocol.Row("comment", "c", new BugComment { Id = "c", BugId = "b", Content = "record" })]));
        db.Close(); db.OpenAccount("test"); Assert.False(db.Ready); Assert.Equal(2, db.Manifest().Length);
        fixture.Replace(); Assert.True(db.Ready); Assert.Empty(db.Read<Bug>("bug")); Assert.Empty(db.Read<BugComment>("comment"));
        db.OpenAccount("other-account"); Assert.False(db.Ready); Assert.Empty(db.Manifest());
    }
    [Fact]
    public async Task Failed_write_and_success_without_push_do_not_change_local_data()
    {
        using var fixture = new SyncTestWorkspace(); fixture.Replace(Bug("old"));
        var sync = new TestWorkspaceSync { Barrier = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{\"code\":200,\"data\":{\"id\":\"b\",\"title\":\"http result\"}}")));
        var api = new BrigitteApi(transport, new TestSession(), new(fixture.Database), sync);
        var writing = api.UpdateBugAsync("b", new() { Title = "new" });
        Assert.False(writing.IsCompleted); Assert.Equal("old", (await api.DetailAsync("b")).Bug.Title);
        fixture.Replace(Bug("new")); sync.Barrier.SetResult(); await writing;
        Assert.Equal("new", (await api.DetailAsync("b")).Bug.Title); Assert.Equal(1, transport.Count);
        sync.CanWrite = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.CreateBugAsync(new())); Assert.Equal(1, transport.Count);
        using var failed = new TestTransport(_ => Task.FromResult(TestTransport.Json("{\"code\":500,\"message\":\"failed\"}", System.Net.HttpStatusCode.InternalServerError)));
        sync.CanWrite = true; var failedApi = new BrigitteApi(failed, new TestSession(), new(fixture.Database), sync);
        await Assert.ThrowsAsync<HttpRequestException>(() => failedApi.UpdateBugAsync("b", new() { Title = "should not appear" }));
        Assert.Equal("new", (await failedApi.DetailAsync("b")).Bug.Title); Assert.Equal(1, sync.Confirmations);
    }
    [Fact]
    public async Task All_reads_use_sqlite_including_details_statistics_prompts_and_directory()
    {
        using var fixture = new SyncTestWorkspace();
        fixture.Replace(Bug("local"), SyncProtocol.Row("project", "p", new Project { Id = "p" }),
            SyncProtocol.Row("user", "u", new IdentityUser { Id = "u" }), SyncProtocol.Row("access", "current", new CurrentAccess()),
            SyncProtocol.Row("prompt", "fix", new LocalPromptTemplate { Key = "fix", Content = "Hi {{name}}", Category = "bug", Enabled = true, ApplicableStatuses = ["waiting"] }));
        using var transport = new TestTransport(_ => throw new InvalidOperationException("本地读取不允许 HTTP"));
        var api = new BrigitteApi(transport, new TestSession(), new(fixture.Database), new TestWorkspaceSync { CanWrite = false });
        Assert.Single(await api.ProjectsAsync(TestContext.Current.CancellationToken)); Assert.Single(await api.BugsAsync("p", TestContext.Current.CancellationToken)); Assert.Equal("local", (await api.DetailAsync("b")).Bug.Title);
        Assert.Equal(1, Assert.Single(await api.GetAsync<List<ProjectStatistics>>("bugs/statistics", TestContext.Current.CancellationToken)).Total);
        Assert.Single(await api.AllAsync<IdentityUser>("identity/users", TestContext.Current.CancellationToken)); Assert.Single(await api.ActionsAsync("waiting"));
        Assert.Equal("Hi world", await api.PromptAsync("fix", new() { ["name"] = "world" })); Assert.Equal(0, transport.Count);
    }
}
