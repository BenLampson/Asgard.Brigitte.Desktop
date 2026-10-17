using System.Collections.Concurrent;
using Asgard.Brigitte.Desktop.Core.Models;
namespace Asgard.Brigitte.Desktop.UI;
/// <summary>一次性 Key 仅在内存中传递，不写入配置或日志。</summary>
public static class KeyResultStore
{
    private static readonly ConcurrentDictionary<string, CreatedMcpKey> Values = new();
    /// <summary>转交给一次性结果窗口。</summary>
    public static string Put(CreatedMcpKey value) { var id = Guid.NewGuid().ToString(); Values[id] = value; return id; }
    /// <summary>消费后删除。</summary>
    public static CreatedMcpKey? Take(string id) => Values.TryRemove(id, out var value) ? value : null;
    /// <summary>退出时清除未消费值。</summary>
    public static void Clear() => Values.Clear();
}
