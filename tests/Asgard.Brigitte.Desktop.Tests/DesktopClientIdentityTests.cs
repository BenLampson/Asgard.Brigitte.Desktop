using Asgard.Brigitte.Desktop.Core;
using Microsoft.Extensions.DependencyInjection;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>只对 Brigitte 请求声明客户端能力，不把业务标识发送给身份提供方。</summary>
public sealed class DesktopClientIdentityTests
{
    [Fact]
    public void Business_http_uses_same_headers_as_realtime_and_oidc_remains_separate()
    {
        var options = new ClientOptions { ClientVersion = "2.3.4+5", ClientPlatform = "windows" };
        var services = new ServiceCollection();
        services.AddLogging(); services.AddBrigitteDesktopHttp(options);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var business = factory.CreateClient("brigitte");
        using var oidc = factory.CreateClient("oidc");
        foreach (var header in DesktopClientIdentity.Headers(options))
        {
            Assert.Equal(header.Value, Assert.Single(business.DefaultRequestHeaders.GetValues(header.Key)));
            Assert.False(oidc.DefaultRequestHeaders.Contains(header.Key));
        }
        Assert.Equal("bug-workflow-v2", Assert.Single(business.DefaultRequestHeaders.GetValues("X-Brigitte-Capabilities")));
        Assert.Equal("2.3.4+5", Assert.Single(business.DefaultRequestHeaders.GetValues("X-Brigitte-Client-Version")));
    }
}
