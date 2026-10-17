using System.Net;
using System.Net.Sockets;
using System.Text;
using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>实际 loopback HTTP 响应先显示等待，官方校验完成后才显示最终结果。</summary>
public sealed class LoginCallbackTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Callback_has_brand_and_close_hint_then_displays_final_result(bool success)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); stopping.CancelAfter(TimeSpan.FromSeconds(10));
        using var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var callback = $"http://127.0.0.1:{port}/callback";
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
        Task<HttpResponseMessage>? received = null;
        var browser = new LoopbackLoginBrowser(_ => { received = http.GetAsync(callback + "?code=test-code&state=test-state", HttpCompletionOption.ResponseHeadersRead, stopping.Token); return Task.FromResult(true); }, "<svg xmlns='http://www.w3.org/2000/svg'></svg>");
        var parameters = await browser.AuthorizeAsync(callback, "https://issuer.test/authorize?state=test-state", stopping.Token);
        using var response = await received!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains(parameters, x => x.Key == "code" && x.Value == "test-code");
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType); Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(stopping.Token));
        var initial = new StringBuilder();
        while (true) { var line = await reader.ReadLineAsync(stopping.Token); Assert.NotNull(line); initial.AppendLine(line); if (line.Contains("history.replaceState")) break; }
        var html = initial.ToString(); Assert.Contains("data:image/svg+xml;base64,", html); Assert.Contains("关闭此页面", html); Assert.Contains("正在完成登录", html); Assert.DoesNotContain("登录成功", html); Assert.DoesNotContain("test-code", html); Assert.DoesNotContain("test-state", html);
        await browser.CompleteAsync(success);
        var final = await reader.ReadToEndAsync(stopping.Token);
        // 最终状态作为 JSON 字符串注入脚本，先还原转义再核对可见文案。
        var decodedFinal = System.Text.RegularExpressions.Regex.Unescape(final);
        Assert.Contains(success ? "登录成功" : "登录未完成", decodedFinal); Assert.DoesNotContain(success ? "登录未完成" : "登录成功", decodedFinal);
        if (Environment.GetEnvironmentVariable("BRIGITTE_VALIDATE_CALLBACK") == "1")
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            var logo = await File.ReadAllTextAsync(Path.Combine(root, "src/Asgard.Brigitte.Desktop/Resources/AppIcon/appicon.svg"), stopping.Token);
            var page = new LoginCallbackPage(); var output = Path.Combine(root, "tests/Asgard.Brigitte.Desktop.Tests/obj/ui-validation"); Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "callback-" + success + ".html"), page.Render(logo) + page.Complete(success), stopping.Token);
        }
    }
}
