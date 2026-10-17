namespace Asgard.Brigitte.Desktop.Core;
/// <summary>原生系统浏览器和一次性 loopback 回调。</summary>
public interface ILoginBrowser
{
    /// <summary>监听启动后再打开浏览器，不向 WebView 暴露回调。</summary>
    Task<IReadOnlyList<KeyValuePair<string, string>>> AuthorizeAsync(string redirectUri, string authorizationUrl, CancellationToken cancellationToken);
    /// <summary>只有官方 SDK 校验和会话提交完成后才报告最终登录结果。</summary>
    Task CompleteAsync(bool success) => Task.CompletedTask;
}
