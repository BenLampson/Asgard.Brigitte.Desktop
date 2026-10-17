namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>现有接口的 CurrentAccess 数据契约。</summary>
public sealed class CurrentAccess
{
    /// <summary>接口字段 TenantId。</summary>
    public string TenantId { get; set; } = "";
    /// <summary>接口字段 UserId。</summary>
    public string UserId { get; set; } = "";
    /// <summary>接口字段 UserType。</summary>
    public string UserType { get; set; } = "";
    /// <summary>接口字段 IsTenantAdmin。</summary>
    public bool IsTenantAdmin { get; set; }
}
