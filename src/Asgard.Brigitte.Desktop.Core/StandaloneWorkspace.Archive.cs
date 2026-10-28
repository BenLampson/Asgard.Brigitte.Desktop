using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本机模式在同一把锁中比较备份快照与待删除记录。</summary>
public sealed partial class StandaloneWorkspace
{
    private readonly Dictionary<string, (string BugId, string Hash, DateTimeOffset Expires)> _archiveTickets = [];

    /// <summary>仅生成临时备份；文件未保存或取消确认时不会删除数据。</summary>
    public BugArchive PrepareArchive(string id)
    {
        lock (_gate)
        {
            var snapshot = ArchiveSnapshot(id);
            var bytes = LocalBugArchivePackage.Create(snapshot);
            var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _archiveTickets.Clear();
            _archiveTickets[ticket] = (id, ArchiveHash(snapshot), DateTimeOffset.UtcNow.AddMinutes(20));
            return new BugArchive($"bug-{id}-backup.zip", Convert.ToBase64String(bytes), ticket);
        }
    }

    private LocalBugArchiveSnapshot ArchiveSnapshot(string id)
    {
        var bug = RequireBug(id);
        if (bug.Status != "closed") throw new InvalidOperationException(ClientText.T("只有已关闭的 BUG 可以删除。"));
        if (BugDisplay.HasLock(bug)) throw new InvalidOperationException(ClientText.T("有处理锁时请先释放锁再删除。"));
        var attachments = Read<BugAttachment>("attachment").Where(x => x.BugId == id).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        foreach (var item in attachments)
        {
            using var command = Connection.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = "SELECT content FROM attachment_data WHERE id=$id";
            command.Parameters.AddWithValue("$id", item.Id);
            var bytes = command.ExecuteScalar() as byte[] ?? throw new InvalidDataException(ClientText.T("附件正文缺失。"));
            item.Base64Content = Convert.ToBase64String(bytes);
        }
        return new LocalBugArchiveSnapshot(bug,
            Read<BugComment>("comment").Where(x => x.BugId == id).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            Read<BugHistory>("history").Where(x => x.BugId == id).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(), attachments);
    }

    private static string ArchiveHash(LocalBugArchiveSnapshot snapshot) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot, Json)));

    private object DeleteArchivedBug(string id, JsonElement input, SqliteTransaction transaction)
    {
        var key = Property(input, "confirmationToken") ?? "";
        if (!_archiveTickets.Remove(key, out var ticket) || ticket.BugId != id || ticket.Expires <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException(ClientText.T("备份确认已失效，请重新下载备份。"));
        var snapshot = ArchiveSnapshot(id);
        if (ticket.Hash != ArchiveHash(snapshot)) throw new InvalidOperationException(ClientText.T("BUG 或关联数据已变更，请重新下载备份。"));
        foreach (var item in snapshot.Attachments) DeleteAttachment(item.Id, transaction);
        foreach (var item in snapshot.Comments) Delete("comment", item.Id, transaction);
        foreach (var item in snapshot.Histories) Delete("history", item.Id, transaction);
        Delete("bug", id, transaction);
        return new { };
    }
}
