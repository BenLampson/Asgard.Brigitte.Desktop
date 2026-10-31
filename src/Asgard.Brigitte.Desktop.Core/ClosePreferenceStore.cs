namespace Asgard.Brigitte.Desktop.Core;

/// <summary>主窗口关闭行为属于设备偏好，在更换发布目录和账号后继续保留。</summary>
public sealed class ClosePreferenceStore
{
    private readonly string _path;
    private readonly LocalPreferenceDatabase? _database;
    /// <summary>首次使用或配置不可读时继续询问，不默认退出或隐藏。</summary>
    public string Behavior { get; private set; } = "ask";
    public ClosePreferenceStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(path))
            {
                var value = JsonSerializer.Deserialize<string>(File.ReadAllText(path));
                if (value is "ask" or "tray" or "exit") Behavior = value;
            }
        }
        catch (JsonException) { /* 损坏的偏好恢复为询问，不阻止应用启动。 */ }
        catch (IOException) { /* 暂时不可读时不推断用户选择。 */ }
        catch (UnauthorizedAccessException) { /* 无读取权限时继续询问。 */ }
    }
    /// <summary>迁移窗口关闭行为至 SQLite，旧文件不删除。</summary>
    public ClosePreferenceStore(LocalPreferenceDatabase database, string legacyPath)
    {
        _path = legacyPath; _database = database;
        try { database.ImportLegacy("close", legacyPath, raw => new Dictionary<string, string> { ["close/behavior"] = raw }); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Microsoft.Data.Sqlite.SqliteException) { }
        try
        {
            if (database.ReadPrefix("close/").TryGetValue("close/behavior", out var raw))
            {
                var value = JsonSerializer.Deserialize<string>(raw);
                if (value is "ask" or "tray" or "exit") Behavior = value;
            }
        }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Microsoft.Data.Sqlite.SqliteException) { }
    }
    /// <summary>先原子落盘，再更新当前行为；保存失败由界面明确告知。</summary>
    public void Set(string behavior)
    {
        if (behavior is not ("ask" or "tray" or "exit")) throw new ArgumentException(ClientText.T("关闭行为无效。"), nameof(behavior));
        if (_database is not null) _database.Set("close/behavior", JsonSerializer.Serialize(behavior));
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(behavior));
            File.Move(temporary, _path, true);
        }
        Behavior = behavior;
    }
}
