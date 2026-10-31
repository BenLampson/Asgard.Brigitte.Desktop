namespace Asgard.Brigitte.Desktop.Core;

/// <summary>登录服务返回的时间与本机时间存在可观测偏差，不判断服务端时钟是否正确。</summary>
public sealed class ClientClockSkewException(TimeSpan difference, Exception innerException)
    : Exception("本机时间与登录服务时间不一致。", innerException)
{
    /// <summary>本机 UTC 时间减去登录服务返回的时间；正值表示本机较快。</summary>
    public TimeSpan Difference { get; } = difference;
}
