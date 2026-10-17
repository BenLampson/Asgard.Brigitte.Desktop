using System.Net;
using System.Text;
using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>离线 HTTP 测试，不发送生产请求。</summary>
internal sealed class TestTransport(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler, IHttpClientFactory
{
    internal int Count { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Count++; return send(request); }
    public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("https://brigitte.test/") };
    internal static HttpResponseMessage Json(string value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
}
