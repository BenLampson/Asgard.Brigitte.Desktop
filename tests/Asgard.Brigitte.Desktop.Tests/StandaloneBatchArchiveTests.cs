using Asgard.Brigitte.Desktop.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.Sqlite;
using System.IO.Compression;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>专用本机库验证整批备份、完整性校验和原子删除。</summary>
public sealed class StandaloneBatchArchiveTests
{
    [Fact]
    public async Task Batch_archive_is_complete_and_any_change_prevents_the_entire_delete()
    {
        using var local = new StandaloneTestWorkspace(); await local.EnterAsync();
        var project = await local.ProjectAsync(); var otherProject = await local.ProjectAsync();
        var first = await ClosedAsync(local, project.Id, "第一条");
        var second = await ClosedAsync(local, project.Id, "第二条");
        var untouched = await ClosedAsync(local, project.Id, "未选择");
        var other = await ClosedAsync(local, otherProject.Id, "其他项目");
        var open = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "未关闭" });
        await local.Api.CommentAsync(first.Id, "评论一", "comment", "human", null);
        await local.Api.CommentAsync(second.Id, "评论二", "comment", "human", null);
        await local.Api.UploadAsync(first.Id, "一.txt", "text/plain", [1, 2], null);
        await local.Api.UploadAsync(second.Id, "二.txt", "text/plain", [3, 4], null);
        await Assert.ThrowsAsync<ArgumentException>(() => local.Api.PrepareBugBatchArchiveAsync(project.Id, []));
        await Assert.ThrowsAsync<ArgumentException>(() => local.Api.PrepareBugBatchArchiveAsync(project.Id, [first.Id, first.Id]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.PrepareBugBatchArchiveAsync(project.Id, [first.Id, open.Id]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.PrepareBugBatchArchiveAsync(project.Id, [first.Id, other.Id]));
        await local.Api.ClaimAsync(second.Id, "worker");
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.PrepareBugBatchArchiveAsync(project.Id, [first.Id, second.Id]));
        await local.Api.ReleaseAsync(second.Id, "worker");

        var archive = await local.Api.PrepareBugBatchArchiveAsync(project.Id, [second.Id, first.Id]);
        await CheckPackageAsync(local.DirectoryPath, archive, first.Id, second.Id);
        await local.Api.CommentAsync(second.Id, "备份之后的新评论", "comment", "human", null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.DeleteArchivedBugsAsync(archive.ConfirmationToken));
        Assert.NotNull(await local.Api.DetailAsync(first.Id)); Assert.NotNull(await local.Api.DetailAsync(second.Id));

        archive = await local.Api.PrepareBugBatchArchiveAsync(project.Id, [first.Id, second.Id]);
        await local.Api.StatusAsync(second.Id, "reopened", "复查", null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.Api.DeleteArchivedBugsAsync(archive.ConfirmationToken));
        Assert.NotNull(await local.Api.DetailAsync(first.Id)); Assert.NotNull(await local.Api.DetailAsync(second.Id));
        foreach (var status in new[] { "pending_deployment", "pending_regression", "human_verified", "closed" })
            await local.Api.StatusAsync(second.Id, status, "测试", null);
        archive = await local.Api.PrepareBugBatchArchiveAsync(project.Id, [first.Id, second.Id]);
        await local.Api.DeleteArchivedBugsAsync(archive.ConfirmationToken);
        var remaining = await local.Api.BugsAsync(project.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, remaining.Count); Assert.Contains(remaining, item => item.Id == untouched.Id); Assert.Contains(remaining, item => item.Id == open.Id);
        Assert.NotNull(await local.Api.DetailAsync(other.Id));
        Assert.Empty(local.Database.Read<BugAttachment>("attachment"));
        Assert.Equal(0, local.Transport.Count);
    }

    private static async Task<Bug> ClosedAsync(StandaloneTestWorkspace local, string projectId, string title)
    {
        var bug = await local.Api.CreateBugAsync(new() { ProjectId = projectId, Title = title });
        foreach (var status in new[] { "pending_deployment", "pending_regression", "human_verified", "closed" })
            await local.Api.StatusAsync(bug.Id, status, "测试", null);
        return bug;
    }

    private static async Task CheckPackageAsync(string directory, BugArchive archive, string first, string second)
    {
        using var zip = new ZipArchive(new MemoryStream(Convert.FromBase64String(archive.Base64Content)), ZipArchiveMode.Read);
        var sqliteEntry = zip.GetEntry("bugs.sqlite"); var excelEntry = zip.GetEntry("bugs.xlsx");
        Assert.NotNull(sqliteEntry); Assert.NotNull(excelEntry);
        var path = Path.Combine(directory, "batch-backup.sqlite");
        await using (var file = File.Create(path)) await sqliteEntry.Open().CopyToAsync(file, TestContext.Current.CancellationToken);
        using var database = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        database.Open(); using var command = database.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM records WHERE kind='bug'"; Assert.Equal(2L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM attachment_data"; Assert.Equal(2L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM records WHERE kind='bug' AND id IN ($first,$second)";
        command.Parameters.AddWithValue("$first", first); command.Parameters.AddWithValue("$second", second);
        Assert.Equal(2L, command.ExecuteScalar());
        using var buffer = new MemoryStream(); await excelEntry.Open().CopyToAsync(buffer, TestContext.Current.CancellationToken); buffer.Position = 0;
        using var workbook = SpreadsheetDocument.Open(buffer, false);
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(workbook, TestContext.Current.CancellationToken));
        Assert.Equal(3, workbook.WorkbookPart!.WorksheetParts.First().Worksheet.Descendants<Row>().Count());
    }
}
