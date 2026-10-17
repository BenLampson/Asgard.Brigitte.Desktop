namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>私有邀请摘要，随机令牌只出现在创建响应，不持久化。</summary>
public sealed class ProjectInvitation
{
    /// <summary>邀请记录 ID，不是加入凭据。</summary>
    public string Id { get; set; } = "";
    /// <summary>是否需要额外邀请口令。</summary>
    public bool RequiresPassword { get; set; }
    /// <summary>服务器 UTC 到期时间。</summary>
    public DateTime ExpiresAt { get; set; }
    /// <summary>负责人是否已经撤销链接。</summary>
    public bool Revoked { get; set; }
    /// <summary>服务器 UTC 创建时间。</summary>
    public DateTime CreateTime { get; set; }
    /// <summary>只在创建响应中存在，退出窗口后销毁。</summary>
    public string? Token { get; set; }
}
