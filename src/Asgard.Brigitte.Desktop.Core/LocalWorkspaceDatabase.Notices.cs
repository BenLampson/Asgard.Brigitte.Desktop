using Microsoft.Data.Sqlite;

namespace Asgard.Brigitte.Desktop.Core;

public sealed partial class LocalWorkspaceDatabase
{
    /// <summary>通知记录和业务投影一起提交；重连回放不产生历史记录。</summary>
    private void SaveNotices(IReadOnlyList<SyncNotice> notices, SqliteTransaction transaction)
    {
        foreach (var notice in notices)
        {
            using var cmd = Connection.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = "INSERT OR IGNORE INTO notices(id,message,time,notice_json) VALUES($id,$message,$time,$json)";
            cmd.Parameters.AddWithValue("$id", notice.Id); cmd.Parameters.AddWithValue("$message", notice.EnglishMessage is null ? notice.Message : JsonSerializer.Serialize(new[] { notice.Message, notice.EnglishMessage }));
            cmd.Parameters.AddWithValue("$time", notice.Time.ToString("O")); cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(notice, Json)); cmd.ExecuteNonQuery();
        }
    }
    public IReadOnlyList<SyncNotice> Notices()
    {
        lock (_gate)
        {
            if (_connection is null || !Ready) return [];
            using var cmd = Connection.CreateCommand(); cmd.CommandText = "SELECT id,message,time,seen,notice_json FROM notices WHERE seen=0 ORDER BY time DESC,rowid DESC";
            var result = new List<(SyncNotice Notice, bool HasMetadata)>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    SyncNotice? structured = null;
                    if (!reader.IsDBNull(4))
                    {
                        try { structured = JsonSerializer.Deserialize<SyncNotice>(reader.GetString(4), Json); }
                        catch (JsonException) { /* 仍可读取原始通知。 */ }
                    }
                    if (structured is not null) { result.Add((structured, true)); continue; }
                    var message = reader.GetString(1);
                    string? english = null;
                    if (message.StartsWith('['))
                    {
                        try
                        {
                            var pair = JsonSerializer.Deserialize<string[]>(message);
                            if (pair is { Length: 2 } && pair.All(x => x is not null)) { message = pair[0]; english = pair[1]; }
                        }
                        catch (JsonException) { /* 兼容升级前的纯文本动态。 */ }
                    }
                    result.Add((new(reader.GetString(0), message, DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture), reader.GetInt32(3) != 0) { EnglishMessage = english }, false));
                }
            }
            return result.Select(item => FilterNotice(item.Notice, item.HasMetadata)).OfType<SyncNotice>().ToArray();
        }
    }
    /// <summary>按当前规则逐项重建合并动态；规则修改后不能泄露已关闭的类别。</summary>
    private SyncNotice? FilterNotice(SyncNotice notice, bool hasMetadata)
    {
        if (noticePreferences is null || _account is null) return notice;
        if (notice.Parts is { Length: > 0 } && notice.Subject is not null)
        {
            var allowed = notice.Parts.Where(part => noticePreferences.Allows(_account, notice with
            {
                Changes = part.Change,
                TargetStatuses = part.TargetStatus is null ? [] : [part.TargetStatus]
            })).ToArray();
            if (allowed.Length == 0) return null;
            return notice with
            {
                Message = notice.Subject + "\n" + string.Join("；", allowed.Select(part => part.Message)),
                EnglishMessage = (notice.EnglishSubject ?? notice.Subject) + "\n" + string.Join("；", allowed.Select(part => part.EnglishMessage ?? part.Message)),
                Changes = allowed.Aggregate(NotificationChange.None, (flags, part) => flags | part.Change),
                TargetStatuses = allowed.Where(part => part.TargetStatus is not null).Select(part => part.TargetStatus!).Distinct().ToArray(),
                Parts = allowed
            };
        }
        return NoticeAllowed(notice, hasMetadata) ? notice : null;
    }
    /// <summary>历史动态没有类型元数据：只在规则没有限制时显示，绝不从通知文案猜类别。</summary>
    private bool NoticeAllowed(SyncNotice notice, bool hasMetadata)
    {
        if (noticePreferences is null || _account is null) return true;
        if (hasMetadata && notice.Changes != NotificationChange.None)
        {
            // 早期合并动态无法拆分，只有所有类别和状态都允许时才显示完整文案。
            var configured = noticePreferences.Get(_account, notice.ProjectId);
            return configured.Enabled && (configured.Changes & notice.Changes) == notice.Changes &&
                ((notice.Changes & NotificationChange.Status) == 0 || configured.Statuses is null ||
                 notice.TargetStatuses.Length > 0 && notice.TargetStatuses.All(status => configured.Statuses.Contains(status, StringComparer.Ordinal)));
        }
        var separator = notice.Id.IndexOf(':');
        var key = separator < 0 ? "" : notice.Id[(separator + 1)..];
        string? projectId = key.StartsWith("project/", StringComparison.Ordinal) ? key[8..] : null;
        if (key.StartsWith("bug/", StringComparison.Ordinal))
        {
            using var cmd = Connection.CreateCommand(); cmd.CommandText = "SELECT json FROM records WHERE key=$key";
            cmd.Parameters.AddWithValue("$key", key);
            if (cmd.ExecuteScalar() is string json)
            {
                try { using var document = JsonDocument.Parse(json); projectId = document.RootElement.TryGetProperty("projectId", out var value) ? value.GetString() : null; }
                catch (JsonException) { /* 历史归属未知时保守处理。 */ }
            }
        }
        if (string.IsNullOrWhiteSpace(projectId)) return !noticePreferences.HasRestrictions(_account);
        var rule = noticePreferences.Get(_account, projectId);
        return rule.Enabled && rule.Changes == NotificationChange.All && rule.Statuses is null;
    }
    /// <summary>全部已读时物理删除通知行，不影响业务记录或同步游标。</summary>
    public void MarkNoticesRead() { lock (_gate) { if (_connection is not null) Execute("DELETE FROM notices"); } }
    /// <summary>单条已读只删除指定通知，不能顺带清除尚未阅读的其它动态。</summary>
    public void MarkNoticeRead(string id)
    {
        lock (_gate)
        {
            if (_connection is null) return;
            using var cmd = Connection.CreateCommand(); cmd.CommandText = "DELETE FROM notices WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id); cmd.ExecuteNonQuery();
        }
    }
}
