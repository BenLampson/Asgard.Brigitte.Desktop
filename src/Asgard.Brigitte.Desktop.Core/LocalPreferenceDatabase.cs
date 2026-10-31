using Microsoft.Data.Sqlite;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>设备偏好使用同一个 SQLite 文件；凭据和工作区业务数据不进入该库。</summary>
public sealed class LocalPreferenceDatabase(string path)
{
    private readonly object _gate = new();

    /// <summary>返回指定前缀的独立副本。</summary>
    public Dictionary<string, string> ReadPrefix(string prefix)
    {
        lock (_gate)
        {
            using var connection = Open(); using var command = connection.CreateCommand();
            command.CommandText = "SELECT key,value FROM preferences WHERE substr(key,1,length($prefix))=$prefix";
            command.Parameters.AddWithValue("$prefix", prefix);
            using var reader = command.ExecuteReader(); var result = new Dictionary<string, string>(StringComparer.Ordinal);
            while (reader.Read()) result[reader.GetString(0)] = reader.GetString(1);
            return result;
        }
    }

    /// <summary>单键替换在事务中提交，失败不修改调用方的内存状态。</summary>
    public void Set(string key, string value)
    {
        lock (_gate)
        {
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            Write(connection, transaction, key, value, false); transaction.Commit();
        }
    }

    /// <summary>单键删除在事务中提交。</summary>
    public void Remove(string key)
    {
        lock (_gate)
        {
            using var connection = Open(); using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM preferences WHERE key=$key"; command.Parameters.AddWithValue("$key", key); command.ExecuteNonQuery();
        }
    }

    /// <summary>只在旧文件可读时一次性导入，不覆盖 SQLite 中已有偏好，也不删除旧文件。</summary>
    public void ImportLegacy(string marker, string legacyPath, Func<string, IReadOnlyDictionary<string, string>> convert)
    {
        lock (_gate)
        {
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            using var check = connection.CreateCommand(); check.Transaction = transaction;
            check.CommandText = "SELECT 1 FROM preferences WHERE key=$key"; check.Parameters.AddWithValue("$key", "migration/" + marker);
            if (check.ExecuteScalar() is not null) return;
            var entries = File.Exists(legacyPath) ? convert(File.ReadAllText(legacyPath)) : new Dictionary<string, string>();
            foreach (var (key, value) in entries) Write(connection, transaction, key, value, true);
            Write(connection, transaction, "migration/" + marker, "1", true);
            transaction.Commit();
        }
    }

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS preferences (key TEXT PRIMARY KEY, value TEXT NOT NULL)";
            command.ExecuteNonQuery(); return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private static void Write(SqliteConnection connection, SqliteTransaction transaction, string key, string value, bool ignore)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = ignore ? "INSERT OR IGNORE INTO preferences(key,value) VALUES($key,$value)" : "INSERT OR REPLACE INTO preferences(key,value) VALUES($key,$value)";
        command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$value", value); command.ExecuteNonQuery();
    }
}
