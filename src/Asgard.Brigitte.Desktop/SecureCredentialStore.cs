using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop;
/// <summary>平台安全存储按 issuer、租户、原生 client 与受众隔离。</summary>
public sealed class SecureCredentialStore(IOptions<ClientOptions> options) : ICredentialStore
{
    private string Key => "brigitte.session." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', options.Value.Authority, options.Value.TenantId, options.Value.ApplicationId, options.Value.ClientId, options.Value.Audience))));
    /// <summary>损坏或系统失效的安全项不当作有效登录。</summary>
    public async Task<string?> ReadAsync() { try { return await SecureStorage.Default.GetAsync(Key); } catch (CryptographicException) { SecureStorage.Default.Remove(Key); return null; } }
    /// <summary>原生 Keychain / Windows 数据保护写入。</summary>
    public Task WriteAsync(string value) => SecureStorage.Default.SetAsync(Key, value);
    /// <summary>只删除当前服务配置的本地会话。</summary>
    public Task ClearAsync() { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}
