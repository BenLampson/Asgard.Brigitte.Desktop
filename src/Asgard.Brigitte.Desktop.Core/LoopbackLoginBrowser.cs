using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Asgard.Brigitte.Desktop.Core;
/// <summary>系统浏览器 PKCE 回调，仅监听本机 IPv4，拒绝无关请求。</summary>
public sealed class LoopbackLoginBrowser(Func<Uri, Task<bool>> openBrowser, string logoSvg) : ILoginBrowser
{
    private TcpClient? _response;
    private LoginCallbackPage? _page;
    private CancellationTokenRegistration _responseCancellation;
    /// <summary>先监听再打开浏览器，取消、超时与完成后均释放监听端口。</summary>
    public async Task<IReadOnlyList<KeyValuePair<string, string>>> AuthorizeAsync(string redirectUri, string authorizationUrl, CancellationToken cancellationToken)
    {
        await CompleteAsync(false);
        var callback = new Uri(redirectUri);
        var expectedState = Parse(new Uri(authorizationUrl).Query).Single(p => p.Key == "state").Value;
        using var listener = new TcpListener(IPAddress.Loopback, callback.Port);
        listener.Start();
        if (!await openBrowser(new Uri(authorizationUrl))) throw new InvalidOperationException("无法打开系统浏览器。");
        while (true)
        {
            var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            var keepResponse = false;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                string? request;
                try { request = await reader.ReadLineAsync(timeout.Token); } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { continue; }
                if (request is null || request.Length > 8192) continue;
                var parts = request.Split(' ');
                var valid = parts.Length == 3 && parts[0] == "GET" && Uri.TryCreate(callback, parts[1], out _);
                var target = valid ? new Uri(callback, parts[1]) : callback;
                var query = valid ? Parse(target.Query) : [];
                valid = valid && target.Authority == callback.Authority && target.AbsolutePath == callback.AbsolutePath && query.Count(p => p.Key == "state") == 1 && query.Any(p => p.Key == "state" && p.Value == expectedState);
                if (!valid) { await WriteAsync(stream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", timeout.Token); continue; }
                var page = new LoginCallbackPage();
                var html = page.Render(logoSvg);
                var headers = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'none'; img-src data:; style-src 'nonce-" + page.Nonce + "'; script-src 'nonce-" + page.Nonce + "'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'\r\nConnection: close\r\n\r\n";
                await WriteAsync(stream, headers + html + "\n", timeout.Token);
                _page = page; _response = connection;
                _responseCancellation = cancellationToken.Register(connection.Dispose);
                keepResponse = true;
                return query;
            }
            finally { if (!keepResponse) connection.Dispose(); }
        }
    }
    /// <summary>浏览器提前关闭不影响客户端登录；页面只接收固定文案，不显示协议错误详情。</summary>
    public async Task CompleteAsync(bool success)
    {
        using var connection = Interlocked.Exchange(ref _response, null);
        var page = _page; _page = null;
        _responseCancellation.Dispose();
        if (connection is null || page is null) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await WriteAsync(connection.GetStream(), page.Complete(success), timeout.Token); }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { /* 用户已经关闭浏览器页面，无需中断已完成的客户端登录。 */ }
    }
    /// <summary>流式 HTML 不保存到磁盘，关闭连接标识最终响应结束。</summary>
    private static async Task WriteAsync(NetworkStream stream, string value, CancellationToken token)
    { await stream.WriteAsync(Encoding.UTF8.GetBytes(value), token); await stream.FlushAsync(token); }
    /// <summary>保留重复参数交由官方 SDK 拒绝，不覆盖 state 或 code。</summary>
    private static List<KeyValuePair<string, string>> Parse(string query) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Select(p => new KeyValuePair<string, string>(Decode(p[0]), p.Length == 2 ? Decode(p[1]) : "")).ToList();
    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
