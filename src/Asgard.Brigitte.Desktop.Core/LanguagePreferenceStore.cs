namespace Asgard.Brigitte.Desktop.Core;

/// <summary>保存设备语言选择；跟随系统始终保留为 system，不固化首次启动时的语言。</summary>
public sealed class LanguagePreferenceStore
{
    private readonly string _path;
    private readonly LocalPreferenceDatabase? _database;
    private readonly Func<string> _systemLanguage;
    public string Mode { get; private set; } = "system";
    public string Language => Mode == "system" ? ClientText.Resolve(_systemLanguage()) : Mode;
    public LanguagePreferenceStore(string path, Func<string>? systemLanguage = null)
    {
        _path = path;
        _systemLanguage = systemLanguage ?? (() => System.Globalization.CultureInfo.CurrentUICulture.Name);
        try
        {
            if (File.Exists(path)) Mode = Normalize(JsonSerializer.Deserialize<string>(File.ReadAllText(path)));
        }
        catch (JsonException) { /* 损坏的偏好回退系统语言。 */ }
        catch (IOException) { /* 暂时不可读不会阻止启动。 */ }
        catch (UnauthorizedAccessException) { /* 无读取权限时使用系统语言。 */ }
    }
    /// <summary>迁移语言偏好至 SQLite；继续动态跟随系统语言。</summary>
    public LanguagePreferenceStore(LocalPreferenceDatabase database, string legacyPath, Func<string>? systemLanguage = null)
    {
        _path = legacyPath; _database = database;
        _systemLanguage = systemLanguage ?? (() => System.Globalization.CultureInfo.CurrentUICulture.Name);
        try { database.ImportLegacy("language", legacyPath, raw => new Dictionary<string, string> { ["language/mode"] = JsonSerializer.Serialize(Normalize(JsonSerializer.Deserialize<string>(raw))) }); }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Microsoft.Data.Sqlite.SqliteException) { }
        try { if (database.ReadPrefix("language/").TryGetValue("language/mode", out var raw)) Mode = Normalize(JsonSerializer.Deserialize<string>(raw)); }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Microsoft.Data.Sqlite.SqliteException) { }
    }
    /// <summary>先原子保存，成功后才切换，避免界面假装记住了选择。</summary>
    public void Set(string mode)
    {
        if (mode is not ("system" or "zh-CN" or "en")) throw new ArgumentException(ClientText.T("语言选项无效。"), nameof(mode));
        if (_database is not null) _database.Set("language/mode", JsonSerializer.Serialize(mode));
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(mode));
            File.Move(temporary, _path, true);
        }
        Mode = mode;
    }
    private static string Normalize(string? mode) => mode is "system" or "zh-CN" or "en" ? mode : "system";
}
