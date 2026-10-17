namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 IdentityUser 数据契约。</summary>
public sealed class IdentityUser
{
    /// <summary>接口字段 Id。</summary>
    public string Id { get; set; } = "";
    /// <summary>接口字段 Username。</summary>
    public string Username { get; set; } = "";
    /// <summary>接口字段 DisplayName。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>接口字段 Email。</summary>
    public string? Email { get; set; }
    /// <summary>接口字段 Status。</summary>
    public int Status { get; set; }
}
