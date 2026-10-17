namespace Asgard.Brigitte.Desktop.Core;

/// <summary>主题是设备偏好，使用稳定路径原子保存，不随发布目录、登录或工作区同步变化。</summary>
public sealed class ThemePreferenceStore
{
    private readonly string _path;
    /// <summary>保留用户选择的模式，而不是将“跟随系统”保存成当前有效颜色。</summary>
    public string Mode { get; private set; }

    /// <summary>首次使用迁移旧版偏好；损坏的配置回退到系统主题。</summary>
    public ThemePreferenceStore(string path, Func<string>? readLegacy = null)
    {
        _path = path;
        Mode = "system";
        if (File.Exists(path))
        {
            try { Mode = Normalize(JsonSerializer.Deserialize<string>(File.ReadAllText(path))); }
            catch (JsonException) { /* 无效偏好不会阻止应用启动。 */ }
            catch (IOException) { /* 暂时不可读时使用系统主题。 */ }
            return;
        }
        Mode = Normalize(readLegacy?.Invoke());
        if (Mode != "system") Set(Mode);
    }

    /// <summary>先落盘再更新内存，保存失败不假装切换成功。</summary>
    public void Set(string mode)
    {
        if (mode is not ("light" or "dark" or "system")) throw new ArgumentException(ClientText.T("主题模式无效。"), nameof(mode));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(mode));
        File.Move(temporary, _path, true);
        Mode = mode;
    }
    /// <summary>拒绝旧配置中未知的主题值。</summary>
    private static string Normalize(string? mode) => mode is "light" or "dark" or "system" ? mode : "system";
}
