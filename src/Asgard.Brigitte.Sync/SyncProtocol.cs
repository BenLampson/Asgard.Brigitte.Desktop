using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Asgard.Brigitte.Sync;

/// <summary>版本化内容指纹协议，不依赖客户端时间或进程内序号，重启后仍可增量对账。</summary>
public static class SyncProtocol
{
    public const int Version = 1;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    public static SyncRow Row(string kind, string id, object value)
    {
        var json = JsonSerializer.Serialize(value, Json);
        return new(kind + "/" + id, Hash(json), json);
    }
    /// <summary>整份授权视图的确定性校验值，包含协议版本、记录键和内容指纹。</summary>
    public static string Cursor(IEnumerable<SyncStamp> manifest)
        => Hash(Version + "\n" + string.Join('\n', manifest.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + ":" + x.Hash)));
    public static SyncRow[] Difference(IReadOnlyDictionary<string, string> previous, IReadOnlyList<SyncRow> current)
    {
        var keys = current.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        return current.Where(x => !previous.TryGetValue(x.Key, out var hash) || hash != x.Hash)
            .Concat(previous.Keys.Where(x => !keys.Contains(x)).Select(x => new SyncRow(x, "", null))).ToArray();
    }
}
