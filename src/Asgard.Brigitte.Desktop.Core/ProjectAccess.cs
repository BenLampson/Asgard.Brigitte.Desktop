using Asgard.Brigitte.Desktop.Core.Models;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>桌面入口可见性；服务端仍负责最终授权。</summary>
public static class ProjectAccess
{
    /// <summary>本机模式可创建项目，在线模式只允许租户管理员创建。</summary>
    public static bool CanCreate(bool isLocal, CurrentAccess access) => isLocal || access.IsTenantAdmin;

    /// <summary>租户管理员或具有明确身份的本项目负责人可管理项目。</summary>
    public static bool CanManage(Project? project, CurrentAccess access)
        => project is not null && (access.IsTenantAdmin
            || !string.IsNullOrWhiteSpace(access.UserId)
            && string.Equals(project.OwnerId, access.UserId, StringComparison.Ordinal));
}
