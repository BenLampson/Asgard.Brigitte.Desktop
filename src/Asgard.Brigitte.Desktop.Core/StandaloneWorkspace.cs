using Microsoft.Data.Sqlite;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>独立的本机业务库；记录和附件正文同事务保存，永不作为在线同步投影。</summary>
public sealed partial class StandaloneWorkspace(string directory) : IDisposable
{
    /// <summary>单机工作区中的稳定身份，不对应任何在线账号。</summary>
    public const string UserId = "local-user";
    private readonly object _gate = new();
    private SqliteConnection? _connection;
    private SqliteTransaction? _transaction;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private SqliteConnection Connection => _connection ?? throw new InvalidOperationException(ClientText.T("本机工作区尚未打开。"));
    /// <summary>成功提交后刷新所有业务窗口；失败事务不产生事件。</summary>
    public event Action? Changed;
    /// <summary>数据库可用后才允许进入工作区。</summary>
    public bool Ready { get { lock (_gate) return _connection is not null; } }

    /// <summary>本机库固定在独立目录，在线缓存和在线账号变化不影响它。</summary>
    public void Open()
    {
        lock (_gate)
        {
            if (_connection is not null) return;
            Directory.CreateDirectory(directory);
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(directory, "workspace.sqlite"), Pooling = false }.ToString());
            try
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA user_version";
                var version = Convert.ToInt32(command.ExecuteScalar());
                if (version > 1) throw new InvalidOperationException(ClientText.T("本机数据库来自更新版本，请升级客户端后再打开。"));
                command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS records(kind TEXT NOT NULL,id TEXT NOT NULL,json TEXT NOT NULL,PRIMARY KEY(kind,id)); CREATE TABLE IF NOT EXISTS attachment_data(id TEXT PRIMARY KEY,content BLOB NOT NULL); PRAGMA user_version=1;";
                command.ExecuteNonQuery();
                _connection = connection;
            }
            catch { connection.Dispose(); throw; }
        }
    }

    /// <summary>查询返回反序列化副本，编辑表单不会修改尚未提交的数据。</summary>
    public List<T> Read<T>(string kind)
    {
        lock (_gate)
        {
            // 本机身份与权限即时生成，语言切换不改写用户业务内容。
            if (kind == "access") return [(T)(object)new CurrentAccess { TenantId = "local", UserId = UserId, UserType = "local" }];
            if (kind == "user") return [(T)(object)new IdentityUser { Id = UserId, DisplayName = ClientText.T("本机用户"), Status = 1 }];
            if (kind == "prompt") return LocalPrompts().Cast<T>().ToList();
            using var command = Connection.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = "SELECT json FROM records WHERE kind=$kind ORDER BY id";
            command.Parameters.AddWithValue("$kind", kind);
            using var reader = command.ExecuteReader();
            var result = new List<T>();
            while (reader.Read()) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), Json)!);
            return result;
        }
    }

    /// <summary>所有本机写操作统一事务化；不支持的在线操作在这里明确拒绝。</summary>
    public T Write<T>(string path, object? body, string method, CancellationToken cancellationToken = default)
    {
        T result;
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = Connection.BeginTransaction();
            _transaction = transaction;
            try
            {
                var parts = path.Split('?', 2);
                var segments = parts[0].Split('/').Select(Uri.UnescapeDataString).ToArray();
                var input = JsonSerializer.SerializeToElement(body, Json);
                var value = Dispatch(segments, parts.Length > 1 ? parts[1] : "", input, method, transaction);
                // 结果转换和取消也必须在提交之前完成，避免调用者误以为失败而重复创建。
                result = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
            }
            finally { _transaction = null; }
        }
        Changed?.Invoke();
        return result;
    }

    /// <summary>附件内容与元数据分别读取，列表不加载大体积二进制。</summary>
    public BugAttachment Download(string bugId, string id)
    {
        lock (_gate)
        {
            RequireBug(bugId);
            var attachment = Read<BugAttachment>("attachment").SingleOrDefault(x => x.Id == id && x.BugId == bugId)
                ?? throw new InvalidOperationException(ClientText.T("附件不存在。"));
            using var command = Connection.CreateCommand();
            command.CommandText = "SELECT content FROM attachment_data WHERE id=$id";
            command.Parameters.AddWithValue("$id", id);
            var bytes = command.ExecuteScalar() as byte[] ?? throw new InvalidDataException(ClientText.T("附件正文缺失。"));
            attachment.Base64Content = Convert.ToBase64String(bytes);
            return attachment;
        }
    }

    /// <summary>仅内部业务代码可持久化完整记录，不接纳输入中的身份与时间字段。</summary>
    private void Save(string kind, string id, object value, SqliteTransaction transaction)
    {
        using var command = Connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO records(kind,id,json) VALUES($kind,$id,$json) ON CONFLICT(kind,id) DO UPDATE SET json=excluded.json";
        command.Parameters.AddWithValue("$kind", kind); command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(value, Json)); command.ExecuteNonQuery();
    }

    /// <summary>级联删除由业务命令在同一个事务中显式完成。</summary>
    private void Delete(string kind, string id, SqliteTransaction transaction)
    {
        using var command = Connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "DELETE FROM records WHERE kind=$kind AND id=$id";
        command.Parameters.AddWithValue("$kind", kind); command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
    }

    /// <summary>本地标识随机生成，不与在线记录互相推断或合并。</summary>
    private static string NewId() => Guid.NewGuid().ToString("N");
    /// <summary>统一长度与必填校验，避免空标题和超长元数据落库。</summary>
    private static string Text(string? value, int max, bool required = false)
    {
        var text = value?.Trim() ?? "";
        if (required && text.Length == 0 || text.Length > max) throw new ArgumentException(ClientText.T("内容为空或超过允许长度。"));
        return text;
    }
    /// <summary>只允许本机身份作为负责人，空值表示未指派。</summary>
    private static string? Assignee(string? id) => string.IsNullOrWhiteSpace(id) ? null : id == UserId ? id
        : throw new ArgumentException(ClientText.T("本机工作区只能选择本机用户。"));
    /// <summary>释放数据库；WAL 由 SQLite 正常关闭处理。</summary>
    public void Dispose() { lock (_gate) { _connection?.Dispose(); _connection = null; } }
}
