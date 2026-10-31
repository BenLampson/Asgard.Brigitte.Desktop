namespace Asgard.Brigitte.Desktop.Core;

public sealed partial class SessionService
{
    /// <summary>仅在令牌校验失败后读取登录服务的时间，不使用未验证的令牌进行诊断。</summary>
    private async Task CheckClockAfterInvalidJwtAsync(HeimdallClientException error, CancellationToken cancellationToken)
    {
        var difference = await TryMeasureClockDifferenceAsync(cancellationToken);
        if (difference is { } value) throw new ClientClockSkewException(value, error);
    }

    /// <summary>通过同一 HTTPS 登录服务的新鲜响应粗略比较时钟；失败时保留原始认证错误。</summary>
    private async Task<TimeSpan?> TryMeasureClockDifferenceAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(options.Value.Authority, UriKind.Absolute, out var authority) ||
            authority.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(authority.UserInfo)) return null;

        var discovery = new Uri(authority.AbsoluteUri.TrimEnd('/') + "/.well-known/openid-configuration");
        using var request = new HttpRequestMessage(HttpMethod.Get, discovery);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            using var client = factory.CreateClient("oidc");
            var startedAt = _time.GetUtcNow();
            var timestamp = _time.GetTimestamp();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var elapsed = _time.GetElapsedTime(timestamp);
            var receivedAt = _time.GetUtcNow();
            if (!response.IsSuccessStatusCode || response.Headers.Date is not { } serverTime ||
                response.Headers.Age is { } age && age > TimeSpan.Zero ||
                Math.Abs((receivedAt - startedAt - elapsed).TotalSeconds) > 2) return null;

            // HTTP Date 精确到秒；往返延迟越大，不确定范围越大。
            var difference = receivedAt - TimeSpan.FromTicks(elapsed.Ticks / 2) - serverTime;
            var uncertainty = TimeSpan.FromTicks(elapsed.Ticks / 2) + TimeSpan.FromSeconds(1);
            var tolerance = TimeSpan.FromSeconds(options.Value.OAuthOptions().ClockSkewSeconds);
            return difference.Duration() > tolerance + uncertainty ? difference : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException) { return null; }
    }
}
