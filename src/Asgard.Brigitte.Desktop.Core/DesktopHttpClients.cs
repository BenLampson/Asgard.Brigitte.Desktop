using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
namespace Asgard.Brigitte.Desktop.Core;
/// <summary>原生客户端共用的 HTTP 装配，可在不启动 MAUI 的情况下验证真实处理管线。</summary>
public static class DesktopHttpClients
{
    /// <summary>关闭重定向与 Cookie，写请求不重试，熔断窗口覆盖至少两次单次超时。</summary>
    public static IServiceCollection AddBrigitteDesktopHttp(this IServiceCollection services, ClientOptions options)
    {
        services.AddHttpClient("oidc", c => c.Timeout = TimeSpan.FromSeconds(45))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddHttpClient("brigitte", c => { c.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/"); c.Timeout = TimeSpan.FromSeconds(90); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.DisableForUnsafeHttpMethods();
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            });
        return services;
    }
}
