namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>已登录用户持有有效链接后看到的项目摘要。</summary>
public sealed class ProjectInvitationPreview
{
    /// <summary>服务端解析出的目标项目。</summary>
    public string ProjectId { get; set; } = "";
    /// <summary>加入前明确展示的项目名称。</summary>
    public string ProjectName { get; set; } = "";
    /// <summary>是否需要输入负责人设置的口令。</summary>
    public bool RequiresPassword { get; set; }
    /// <summary>服务器 UTC 到期时间。</summary>
    public DateTime ExpiresAt { get; set; }
    /// <summary>当前认证用户是否已在成员集合内。</summary>
    public bool AlreadyMember { get; set; }
}
