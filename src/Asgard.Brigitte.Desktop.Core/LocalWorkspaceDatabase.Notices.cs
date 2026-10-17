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
            cmd.CommandText = "INSERT OR IGNORE INTO notices(id,message,time) VALUES($id,$message,$time)";
            cmd.Parameters.AddWithValue("$id", notice.Id); cmd.Parameters.AddWithValue("$message", notice.Message);
            cmd.Parameters.AddWithValue("$time", notice.Time.ToString("O")); cmd.ExecuteNonQuery();
        }
    }
    public IReadOnlyList<SyncNotice> Notices()
    {
        lock (_gate)
        {
            if (_connection is null || !Ready) return [];
            using var cmd = Connection.CreateCommand(); cmd.CommandText = "SELECT id,message,time,seen FROM notices WHERE seen=0 ORDER BY time DESC,rowid DESC";
            using var reader = cmd.ExecuteReader(); var result = new List<SyncNotice>();
            while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture), reader.GetInt32(3) != 0));
            return result;
        }
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
