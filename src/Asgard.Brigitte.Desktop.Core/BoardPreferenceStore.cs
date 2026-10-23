using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>泳道显示顺序只保存在本机，按服务、租户、账号及项目隔离。</summary>
public sealed class BoardPreferenceStore
{
    private readonly string _path;
    private Dictionary<string, string[]> _orders = [];

    /// <summary>无法读取的偏好使用默认顺序，不影响工作区启动。</summary>
    public BoardPreferenceStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(path)) _orders = (JsonSerializer.Deserialize<Dictionary<string, string[]?>>(File.ReadAllText(path)) ?? [])
                .Where(x => x.Value is not null).ToDictionary(x => x.Key, x => Normalize(x.Value!));
        }
        catch (JsonException) { /* 损坏时恢复默认显示。 */ }
        catch (IOException) { /* 暂时不可读时恢复默认显示。 */ }
        catch (UnauthorizedAccessException) { /* 不阻断工作区。 */ }
    }
    /// <summary>过滤废弃项和重复项，升级后的新状态自动补入，不丢失泳道。</summary>
    public static string[] Normalize(IEnumerable<string> order) => order.Concat(BugDisplay.BoardStatuses)
        .Where(x => x is not null && BugDisplay.Statuses.ContainsKey(x)).Distinct(StringComparer.Ordinal).ToArray();
    /// <summary>返回副本，界面排序不会提前修改已保存配置。</summary>
    public string[] Get(string account, string project) => Normalize(_orders.GetValueOrDefault(Key(account, project)) ?? []);
    /// <summary>先原子写入再更新内存，保存失败仍保持上一次顺序。</summary>
    public void Set(string account, string project, IEnumerable<string> order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        var updated = new Dictionary<string, string[]>(_orders) { [Key(account, project)] = Normalize(order) };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(updated));
        File.Move(_path + ".tmp", _path, true);
        _orders = updated;
    }
    /// <summary>不在偏好文件存放原始身份信息。</summary>
    private static string Key(string account, string project) => SyncProtocol.Hash(account) + "/" + project;
}
