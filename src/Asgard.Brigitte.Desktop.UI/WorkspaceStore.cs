using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
namespace Asgard.Brigitte.Desktop.UI;
/// <summary>仅在当前认证会话内共享目录与统计；会话变化立即清空。</summary>
public sealed class WorkspaceStore(BrigitteApi api, SessionService session)
{
    /// <summary>目录提交完成后更新托盘统计。</summary>
    public event Action? Changed;
    /// <summary>可访问项目。</summary>
    public List<Project> Projects { get; private set; } = [];
    /// <summary>目录读取失败不能伪装为用户不存在。</summary>
    public List<IdentityUser> Users { get; private set; } = [];
    /// <summary>项目统计。</summary>
    public List<ProjectStatistics> Statistics { get; private set; } = [];
    /// <summary>后端给出的管理权限。</summary>
    public CurrentAccess Access { get; private set; } = new();
    /// <summary>目录配置或权限错误。</summary>
    public string DirectoryError { get; private set; } = "";
    /// <summary>安全清除，不在退出后保留托盘业务资料。</summary>
    public void Clear() { Projects = []; Users = []; Statistics = []; Access = new(); DirectoryError = ""; }
    /// <summary>每次读取都受 API 的会话版本保护。</summary>
    public async Task LoadAsync()
    {
        var version = session.IdentityVersion;
        var projects = await api.ProjectsAsync();
        var statistics = await api.GetAsync<List<ProjectStatistics>>("bugs/statistics");
        var access = await api.GetAsync<CurrentAccess>("identity/access");
        List<IdentityUser> users;
        string directoryError;
        try { users = await api.AllAsync<IdentityUser>("identity/users"); directoryError = ""; }
        catch (HttpRequestException) { users = []; directoryError = ClientText.T("用户目录暂不可用，负责人保留原始标识。"); }
        if (version != session.IdentityVersion) return;
        Projects = projects; Statistics = statistics; Access = access; Users = users; DirectoryError = directoryError;
        Changed?.Invoke();
    }
    /// <summary>完整名称只用于显示，找不到也不猜测 AI 身份。</summary>
    public string Assignee(string? id) => BugDisplay.Assignee(id, Users);
    /// <summary>项目计数不能将加载失败视为零。</summary>
    public ProjectStatistics? Stats(string id) => Statistics.FirstOrDefault(x => x.ProjectId == id);
}
