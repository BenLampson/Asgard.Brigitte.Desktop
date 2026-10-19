using System.Text.Json;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Sync;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>使用专用临时 SQLite 验证本机完整业务、重启持久化及在线隔离。</summary>
public sealed class StandaloneWorkspaceTests
{
    /// <summary>注册本机能力后，在线命令仍走原 HTTP 和同步屏障，不写入本机事实库。</summary>
    [Fact]
    public async Task Online_commands_keep_existing_transport_and_barrier_with_local_store_registered()
    {
        using var local = new StandaloneTestWorkspace(); await local.EnterAsync();
        var project = await local.ProjectAsync();
        await local.Session.LogoutAsync();
        using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{\"code\":200,\"data\":{\"id\":\"online-result\",\"title\":\"online bug\"}}")));
        var online = new TestSession(); var sync = new TestWorkspaceSync();
        var api = new BrigitteApi(transport, online, new LocalWorkspaceReader(local.OnlineDatabase, local.Database, online), sync, local.Database);
        Assert.False(api.IsLocal);
        var result = await api.CreateBugAsync(new() { ProjectId = "online-project", Title = "online bug" });
        Assert.Equal("online-result", result.Id); Assert.Equal(1, transport.Count); Assert.Equal(1, sync.Confirmations);
        Assert.Empty(local.Database.Read<Bug>("bug"));
        Assert.Equal(project.Id, Assert.Single(local.Database.Read<Project>("project")).Id);
    }

    /// <summary>项目、BUG、状态、回复、处理锁和附件均可在零网络下完成并重启读取。</summary>
    [Fact]
    public async Task Complete_local_workflow_persists_without_credentials_HTTP_or_sync_barriers()
    {
        using var local = new StandaloneTestWorkspace();
        await local.EnterAsync();
        Assert.True(local.Session.HasWorkspace); Assert.False(local.Session.IsAuthenticated);
        var project = await local.ProjectAsync();
        project = await local.Api.WriteAsync<Project>("projects/" + project.Id, new ProjectInput { Name = "更新后的本机项目", Status = "archived" }, "PUT", TestContext.Current.CancellationToken);
        Assert.Equal("archived", project.Status); Assert.Equal(StandaloneWorkspace.UserId, project.OwnerId);
        var input = new BugInput { ProjectId = project.Id, Title = "断网创建的问题", Content = "复现步骤", Priority = "high", AssigneeId = StandaloneWorkspace.UserId };
        var bug = await local.Api.CreateBugAsync(input);
        input.Title = "编辑后的问题";
        var edited = await local.Api.UpdateBugAsync(bug.Id, input);
        Assert.Equal(bug.CreateTime, edited.CreateTime); Assert.Equal("waiting", edited.Status);
        await local.Api.StatusAsync(bug.Id, "processing", "开始处理", null);
        await local.Api.CommentAsync(bug.Id, "回复内容", "comment", "human", null);
        await local.Api.CommentAsync(bug.Id, "处理记录", "process", "human", null);
        await local.Api.ClaimAsync(bug.Id, "worker-local");
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.StatusAsync(bug.Id, "fixed", "", "another-worker"));
        await local.Api.StatusAsync(bug.Id, "fixed", "本机验证通过", "worker-local");
        await local.Api.ReleaseAsync(bug.Id, "worker-local");
        var attachment = await local.Api.UploadAsync(bug.Id, "中文附件.txt", "text/plain", [0, 1, 2, 255], null);
        Assert.Equal("", attachment.Base64Content);
        Assert.Equal(new byte[] { 0, 1, 2, 255 }, Convert.FromBase64String((await local.Api.DownloadAsync(bug.Id, attachment.Id)).Base64Content));
        var detail = await local.Api.DetailAsync(bug.Id);
        Assert.Equal(2, detail.Comments.Length); Assert.Equal(2, detail.Histories.Length);
        Assert.Single(detail.Attachments); Assert.Empty(detail.Attachments[0].Base64Content);
        var stats = Assert.Single(await local.Api.GetAsync<List<ProjectStatistics>>("bugs/statistics", TestContext.Current.CancellationToken));
        Assert.Equal(1, stats.Total); Assert.Equal(0, stats.Unresolved);
        Assert.Contains("编辑后的问题", await local.Api.PromptAsync("localFixPrompt", new() { ["bugId"] = bug.Id }));
        Assert.NotEmpty(await local.Api.ActionsAsync("fixed"));
        local.Database.Dispose();
        using var reopened = new StandaloneWorkspace(Path.Combine(local.DirectoryPath, "local")); reopened.Open();
        Assert.Equal("编辑后的问题", Assert.Single(reopened.Read<Bug>("bug")).Title);
        Assert.Equal(2, reopened.Read<BugHistory>("history").Count);
        Assert.Equal(new byte[] { 0, 1, 2, 255 }, Convert.FromBase64String(reopened.Download(bug.Id, attachment.Id).Base64Content));
        await local.Session.LogoutAsync();
        Assert.False(local.Session.HasWorkspace);
        using var restarted = new SessionService(Options.Create(new ClientOptions()), local.Transport, local.Credentials, local.Credentials);
        Assert.False(restarted.HasWorkspace);
        Assert.Equal("untouched-online-credentials", local.Credentials.Value);
        Assert.Equal(0, local.Credentials.Writes + local.Credentials.Clears + local.Credentials.BrowserOpens);
        Assert.Equal(0, local.Transport.Count); Assert.Equal(0, local.Sync.Confirmations);
    }

    /// <summary>本机模式不会读取在线缓存，也不能通过未支持命令回退到线上。</summary>
    [Fact]
    public async Task Local_workspace_is_isolated_and_rejects_online_only_features()
    {
        using var local = new StandaloneTestWorkspace();
        local.OnlineDatabase.OpenAccount("verified-online-account");
        var row = SyncProtocol.Row("project", "online-project", new Project { Id = "online-project", Name = "私有在线项目" });
        var begin = new SyncFrame { Phase = "begin", BatchId = "online-batch", BaseCursor = SyncProtocol.Cursor([]), Cursor = SyncProtocol.Cursor([new(row.Key, row.Hash)]), Total = 1 };
        local.OnlineDatabase.Apply(begin); local.OnlineDatabase.Apply(begin with { Phase = "rows", Rows = [row] }); local.OnlineDatabase.Apply(begin with { Phase = "commit" });
        await local.EnterAsync();
        Assert.Empty(await local.Api.ProjectsAsync(TestContext.Current.CancellationToken));
        var project = await local.ProjectAsync();
        Assert.NotEqual("online-project", project.Id);
        Assert.Equal("online-project", Assert.Single(local.OnlineDatabase.Read<Project>("project")).Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.WriteAsync<object>("mcp-api-keys", new { name = "must-not-create" }, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.InvitationRequestAsync<object>("invitations/inspect", new { token = "must-not-send" }, TestContext.Current.CancellationToken));
        await local.Session.InitializeAsync(TestContext.Current.CancellationToken); await local.Session.MaintainAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Session.LoginAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, local.Transport.Count); Assert.Equal(0, local.Sync.Confirmations);
        await local.Session.LogoutAsync();
        Assert.Equal("online-project", Assert.Single(await local.Api.ProjectsAsync(TestContext.Current.CancellationToken)).Id);
        await local.EnterAsync();
        Assert.Equal(project.Id, Assert.Single(await local.Api.ProjectsAsync(TestContext.Current.CancellationToken)).Id);
    }

    /// <summary>失败与取消不落库、不发送刷新；删除会清除 BUG 全部子记录和正文。</summary>
    [Fact]
    public async Task Validation_cancellation_and_cascade_deletion_are_atomic()
    {
        using var local = new StandaloneTestWorkspace(); await local.EnterAsync();
        var project = await local.ProjectAsync();
        var bug = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "测试事务" });
        var changes = 0; local.Database.Changed += () => changes++;
        await Assert.ThrowsAsync<ArgumentException>(() => local.Api.UpdateBugAsync(bug.Id, new() { ProjectId = project.Id, Title = "不应保存", AssigneeId = "online-user" }));
        await Assert.ThrowsAsync<ArgumentException>(() => local.Api.StatusAsync(bug.Id, "invalid", "", null));
        await Assert.ThrowsAsync<FormatException>(() => local.Api.WriteAsync<BugAttachment>("bugs/" + bug.Id + "/attachments", new { fileName = "bad", contentType = "text/plain", base64Content = "not-base64!" }, cancellationToken: TestContext.Current.CancellationToken));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => local.Api.WriteAsync<Project>("projects", new ProjectInput { Name = "取消项目" }, cancellationToken: canceled.Token));
        Assert.Equal(0, changes); Assert.Equal("测试事务", (await local.Api.DetailAsync(bug.Id)).Bug.Title);
        Assert.Empty(local.Database.Read<BugAttachment>("attachment")); Assert.Empty(local.Database.Read<BugHistory>("history"));
        await local.Api.CommentAsync(bug.Id, "测试回复", "comment", "human", null);
        await local.Api.StatusAsync(bug.Id, "closed", "测试历史", null);
        var file = await local.Api.UploadAsync(bug.Id, "delete.txt", "text/plain", [9], null);
        await local.Api.DeleteAttachmentAsync(bug.Id, file.Id, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.DownloadAsync(bug.Id, file.Id));
        await local.Api.UploadAsync(bug.Id, "cascade.txt", "text/plain", [10], null);
        await Assert.ThrowsAsync<ArgumentException>(() => local.Api.WriteAsync<JsonElement>("bugs/" + bug.Id, null, "DELETE", TestContext.Current.CancellationToken));
        await local.Api.ClaimAsync(bug.Id, "worker");
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.DeleteBugAsync(bug.Id));
        await local.Api.ReleaseAsync(bug.Id, "worker");
        await local.Api.DeleteBugAsync(bug.Id);
        Assert.Empty(await local.Api.BugsAsync(project.Id, TestContext.Current.CancellationToken));
        Assert.Empty(local.Database.Read<BugComment>("comment")); Assert.Empty(local.Database.Read<BugHistory>("history")); Assert.Empty(local.Database.Read<BugAttachment>("attachment"));
        using var inspect = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(local.DirectoryPath, "local", "workspace.sqlite"), Pooling = false }.ToString()); inspect.Open();
        using var command = inspect.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM attachment_data";
        Assert.Equal(0L, command.ExecuteScalar());
        Assert.Equal(0, local.Transport.Count);
    }
}
