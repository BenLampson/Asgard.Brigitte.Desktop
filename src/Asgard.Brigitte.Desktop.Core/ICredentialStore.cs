namespace Asgard.Brigitte.Desktop.Core;
/// <summary>由原生宿主实现安全存储，UI 和 WebView 不接触令牌。</summary>
public interface ICredentialStore
{
    /// <summary>读取受保护会话。</summary>
    Task<string?> ReadAsync();
    /// <summary>写入受保护会话。</summary>
    Task WriteAsync(string value);
    /// <summary>删除受保护会话。</summary>
    Task ClearAsync();
}
