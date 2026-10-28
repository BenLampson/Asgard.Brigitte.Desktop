using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本机模式下整批备份、快照验证与事务删除。</summary>
public sealed partial class StandaloneWorkspace
{
    private readonly Dictionary<string, (string ProjectId, string[] BugIds, string Hash, DateTimeOffset Expires)> _batchArchiveTickets = [];

    /// <summary>在同一把锁中读取所选记录；生成备份不会删除数据。</summary>
    public BugArchive PrepareBatchArchive(string projectId, IReadOnlyList<string> bugIds)
    {
        lock (_gate)
        {
            var ids = ValidateArchiveBatch(projectId, bugIds);
            var snapshot = BatchArchiveSnapshot(projectId, ids);
            var bytes = LocalBugArchivePackage.Create(snapshot.Bugs);
            var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _batchArchiveTickets.Clear();
            _batchArchiveTickets[ticket] = (projectId, ids, BatchArchiveHash(snapshot), DateTimeOffset.UtcNow.AddMinutes(20));
            return new BugArchive($"bugs-{projectId}-backup.zip", Convert.ToBase64String(bytes), ticket);
        }
    }

    private LocalBugArchiveBatchSnapshot BatchArchiveSnapshot(string projectId, IReadOnlyList<string> ids)
    {
        var snapshots = ids.Select(ArchiveSnapshot).ToArray();
        if (snapshots.Any(item => item.Bug.ProjectId != projectId))
            throw new InvalidOperationException(ClientText.T("所选 BUG 不属于当前项目，请刷新后重新选择。"));
        return new LocalBugArchiveBatchSnapshot(projectId, snapshots);
    }

    private static string BatchArchiveHash(LocalBugArchiveBatchSnapshot snapshot) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot, Json)));

    private object DeleteArchivedBatch(JsonElement input, SqliteTransaction transaction)
    {
        var key = Property(input, "confirmationToken") ?? "";
        if (!_batchArchiveTickets.Remove(key, out var ticket) || ticket.Expires <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException(ClientText.T("备份确认已失效，请重新下载备份。"));
        var snapshot = BatchArchiveSnapshot(ticket.ProjectId, ticket.BugIds);
        if (ticket.Hash != BatchArchiveHash(snapshot))
            throw new InvalidOperationException(ClientText.T("所选 BUG 或关联数据已变更，请重新备份。"));
        foreach (var item in snapshot.Bugs)
        {
            foreach (var attachment in item.Attachments) DeleteAttachment(attachment.Id, transaction);
            foreach (var comment in item.Comments) Delete("comment", comment.Id, transaction);
            foreach (var history in item.Histories) Delete("history", history.Id, transaction);
            Delete("bug", item.Bug.Id, transaction);
        }
        return new { };
    }

    private static string[] ValidateArchiveBatch(string? projectId, IReadOnlyList<string>? ids)
    {
        if (string.IsNullOrWhiteSpace(projectId) || ids is null || ids.Count == 0 || ids.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(ClientText.T("请选择当前项目中需要删除的已关闭 BUG。"));
        var ordered = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (ordered.Length != ids.Count) throw new ArgumentException(ClientText.T("所选 BUG 中包含重复项，请刷新后重试。"));
        return ordered;
    }
}
