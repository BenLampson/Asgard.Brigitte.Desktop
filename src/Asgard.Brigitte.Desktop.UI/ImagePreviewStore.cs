using Asgard.Brigitte.Desktop.Core.Models;
namespace Asgard.Brigitte.Desktop.UI;
/// <summary>短暂保存当前图片附件，退出时由宿主清空。</summary>
public static class ImagePreviewStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BugAttachment> Items = new();
    /// <summary>仅接受已读取且显式请求预览的附件。</summary>
    public static void Put(BugAttachment item) => Items[item.Id] = item;
    /// <summary>关闭窗口即删除，不形成永久文件缓存。</summary>
    public static BugAttachment? Take(string id) => Items.TryRemove(id, out var item) ? item : null;
    /// <summary>会话失效立即清空。</summary>
    public static void Clear() => Items.Clear();
}
