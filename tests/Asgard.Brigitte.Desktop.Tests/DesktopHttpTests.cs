using System.Net;
using Asgard.Brigitte.Desktop.Core;
using Microsoft.Extensions.DependencyInjection;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>启动原生宿主相同的真实 HTTP 容错管线，避免编译通过却所有请求失败。</summary>
public sealed class DesktopHttpTests
{
    /// <summary>读写均可真正发出，采样和超时配置通过管线的运行期校验。</summary>
    [Theory]
    [InlineData("PUT")]
    [InlineData("POST")]
    public async Task Configured_pipeline_sends_healthy_requests(string method)
    {
        var services = new ServiceCollection(); services.AddBrigitteDesktopHttp(new ClientOptions());
        using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{\"code\":200,\"data\":true}")));
        services.AddHttpClient("brigitte").ConfigurePrimaryHttpMessageHandler(() => transport);
        using var provider = services.BuildServiceProvider(); var api = TestWorkspaceSync.Api(provider.GetRequiredService<IHttpClientFactory>(), new TestSession());
        Assert.True(await api.WriteAsync<bool>("test", null, method, TestContext.Current.CancellationToken)); Assert.Equal(1, transport.Count);
    }
    /// <summary>业务写入即使收到 500 也不能由容错处理器重放。</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Configured_pipeline_does_not_replay_failed_writes(string method)
    {
        var services = new ServiceCollection(); services.AddBrigitteDesktopHttp(new ClientOptions());
        using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{\"code\":500,\"message\":\"failed\"}", HttpStatusCode.InternalServerError)));
        services.AddHttpClient("brigitte").ConfigurePrimaryHttpMessageHandler(() => transport);
        using var provider = services.BuildServiceProvider(); var api = TestWorkspaceSync.Api(provider.GetRequiredService<IHttpClientFactory>(), new TestSession());
        await Assert.ThrowsAsync<HttpRequestException>(() => api.WriteAsync<bool>("test", null, method, TestContext.Current.CancellationToken)); Assert.Equal(1, transport.Count);
    }
}
