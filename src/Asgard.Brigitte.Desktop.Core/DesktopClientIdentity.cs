namespace Asgard.Brigitte.Desktop.Core;

/// <summary>HTTP 与 WebSocket 使用同一套客户端声明；能力不是授权凭据。</summary>
public static class DesktopClientIdentity
{
    /// <summary>只声明已经实现的服务端协议能力，本地界面偏好不进入协议。</summary>
    public static IReadOnlyDictionary<string, string> Headers(ClientOptions options) => new Dictionary<string, string>
    {
        ["X-Brigitte-Client"] = "desktop",
        ["X-Brigitte-Client-Version"] = options.ClientVersion,
        ["X-Brigitte-Client-Platform"] = options.ClientPlatform,
        ["X-Brigitte-Capabilities"] = "bug-workflow-v2"
    };
}
