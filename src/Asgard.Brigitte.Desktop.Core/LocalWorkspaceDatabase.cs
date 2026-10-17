using Asgard.Brigitte.Sync;
using Microsoft.Data.Sqlite;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>服务端数据的 SQLite 只读投影；暂存批次不可见，数据和校验游标在同一事务提交。</summary>
public sealed partial class LocalWorkspaceDatabase(string directory) : IDisposable
{
    private readonly object _gate = new();
    private SqliteConnection? _connection;
    private string? _account;
    private SyncFrame? _batch;
    private int _received;
    private volatile bool _ready;
    public bool Ready { get => _ready; private set => _ready = value; }
    public int Received { get { lock (_gate) return _received; } }
    public int Total { get { lock (_gate) return _batch?.Total ?? _received; } }
    public event Action<SyncCommit>? Committed;
    private SqliteConnection Connection => _connection ?? throw new InvalidOperationException(ClientText.T("工作区尚未连接，请等待同步。"));

    /// <summary>按服务地址、租户与已验证用户隔离数据库；重新启动必须经服务端对账才能开放工作区。</summary>
    public void OpenAccount(string account)
    {
        lock (_gate)
        {
            if (_account == account) return;
            Close();
            Directory.CreateDirectory(directory);
            _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, SyncProtocol.Hash(account) + ".sqlite"), Pooling = false }.ToString());
            _connection.Open();
            Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS records (key TEXT PRIMARY KEY, hash TEXT NOT NULL, json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL); CREATE TABLE IF NOT EXISTS staging (key TEXT PRIMARY KEY, hash TEXT NOT NULL, json TEXT); DELETE FROM staging;");
            Execute("CREATE TABLE IF NOT EXISTS notices (id TEXT PRIMARY KEY, message TEXT NOT NULL, time TEXT NOT NULL, seen INTEGER NOT NULL DEFAULT 0)");
            // 兼容旧客户端留下的已读标记，升级后不再保留这些通知行。
            Execute("DELETE FROM notices WHERE seen<>0");
            _account = account;
        }
    }
    public void Close()
    {
        lock (_gate) { _connection?.Dispose(); _connection = null; _account = null; _batch = null; _received = 0; Ready = false; }
    }
    public SyncStamp[] Manifest()
    {
        lock (_gate)
        {
            using var cmd = Connection.CreateCommand(); cmd.CommandText = "SELECT key,json FROM records ORDER BY key";
            using var reader = cmd.ExecuteReader(); var rows = new List<SyncStamp>();
            // 本地缓存不作权威，重新计算指纹以修复意外损坏或旧版本写入的内容。
            while (reader.Read()) rows.Add(new(reader.GetString(0), SyncProtocol.Hash(reader.GetString(1))));
            return rows.ToArray();
        }
    }
    public List<T> Read<T>(string kind)
    {
        lock (_gate)
        {
            if (!Ready) throw new InvalidOperationException(ClientText.T("工作区正在同步，请稍候。"));
            using var cmd = Connection.CreateCommand(); cmd.CommandText = "SELECT json FROM records WHERE key LIKE $prefix ORDER BY key";
            cmd.Parameters.AddWithValue("$prefix", kind + "/%");
            using var reader = cmd.ExecuteReader(); var rows = new List<T>();
            while (reader.Read()) rows.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), Json)!);
            return rows;
        }
    }
    private static readonly JsonSerializerOptions Json = CreateJson();
    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
        options.Converters.Add(new UtcDateTimeConverter()); return options;
    }
    /// <summary>连接中断丢弃未提交批次，已提交的旧数据可继续只读。</summary>
    public void AbortBatch()
    {
        lock (_gate)
        {
            try { if (_connection is not null) Execute("DELETE FROM staging"); }
            catch (SqliteException) { /* 磁盘暂不可写时留待下一次 begin 清理，不能使后台同步任务永久退出。 */ }
            finally { _batch = null; _received = 0; }
        }
    }
    public void Apply(SyncFrame frame)
    {
        SyncCommit? committed = null;
        lock (_gate)
        {
            if (frame.Phase == "begin")
            {
                if (frame.Total < 0 || SyncProtocol.Cursor(Manifest()) != frame.BaseCursor) throw new InvalidDataException(ClientText.T("同步基线不一致，正在重新对账。"));
                Execute("DELETE FROM staging"); _batch = frame; _received = 0; return;
            }
            var batch = _batch ?? throw new InvalidDataException(ClientText.T("同步批次缺少起始帧。"));
            if (frame.BatchId != batch.BatchId || frame.Cursor != batch.Cursor || frame.BaseCursor != batch.BaseCursor || frame.Total != batch.Total)
                throw new InvalidDataException(ClientText.T("同步帧顺序无效。"));
            if (frame.Phase == "rows")
            {
                using var transaction = Connection.BeginTransaction();
                foreach (var row in frame.Rows)
                {
                    if (row.Json is not null && SyncProtocol.Hash(row.Json) != row.Hash) throw new InvalidDataException(ClientText.T("同步内容校验失败。"));
                    using var cmd = Connection.CreateCommand(); cmd.Transaction = transaction;
                    cmd.CommandText = "INSERT INTO staging(key,hash,json) VALUES($key,$hash,$json)";
                    cmd.Parameters.AddWithValue("$key", row.Key); cmd.Parameters.AddWithValue("$hash", row.Hash); cmd.Parameters.AddWithValue("$json", (object?)row.Json ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit(); _received += frame.Rows.Length;
                if (_received > batch.Total) throw new InvalidDataException(ClientText.T("同步记录数超出清单。"));
                return;
            }
            if (frame.Phase is not ("commit" or "denied") || _received != batch.Total) throw new InvalidDataException(ClientText.T("同步批次不完整。"));
            var notices = batch.Replay || frame.Phase == "denied" ? [] : BuildNotices(batch.BatchId);
            using (var transaction = Connection.BeginTransaction())
            {
                Execute("DELETE FROM records WHERE key IN (SELECT key FROM staging WHERE json IS NULL); INSERT OR REPLACE INTO records SELECT key,hash,json FROM staging WHERE json IS NOT NULL;", transaction);
                // 同时校验未修改的记录，丢帧或过期批次都不能推进游标。
                using var check = Connection.CreateCommand(); check.Transaction = transaction; check.CommandText = "SELECT key,json FROM records";
                var stamps = new List<SyncStamp>();
                using (var reader = check.ExecuteReader()) while (reader.Read()) stamps.Add(new(reader.GetString(0), SyncProtocol.Hash(reader.GetString(1))));
                if (SyncProtocol.Cursor(stamps) != batch.Cursor) throw new InvalidDataException(ClientText.T("同步清单校验失败。"));
                using var cursor = Connection.CreateCommand(); cursor.Transaction = transaction;
                cursor.CommandText = "INSERT OR REPLACE INTO metadata(key,value) VALUES('cursor',$cursor)"; cursor.Parameters.AddWithValue("$cursor", batch.Cursor); cursor.ExecuteNonQuery();
                if (frame.Phase == "denied") Execute("DELETE FROM notices", transaction);
                else SaveNotices(notices, transaction);
                Execute("DELETE FROM staging", transaction); transaction.Commit();
            }
            Ready = frame.Phase != "denied"; _batch = null;
            committed = new(batch.Replay, notices, frame.Phase == "denied" ? [] : batch.Receipts);
        }
        Committed?.Invoke(committed);
    }
    private void Execute(string sql, SqliteTransaction? transaction = null)
    { using var cmd = Connection.CreateCommand(); cmd.Transaction = transaction; cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
    public void Dispose() => Close();
}
