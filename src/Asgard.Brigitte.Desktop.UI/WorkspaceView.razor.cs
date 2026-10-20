using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Options;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
namespace Asgard.Brigitte.Desktop.UI;
/// <summary>保留主看板状态，所有新增、详情与编辑由宿主打开独立窗口。</summary>
public partial class WorkspaceView
{
    /// <summary>主窗口初始上下文。</summary>
    [Parameter] public DesktopView View { get; set; } = new(DesktopViewKind.Main);
    /// <summary>可信的站点外链配置。</summary>
    [Inject] public IOptions<ClientOptions> Options { get; set; } = null!;
    private List<Bug> _bugs = [];
    private Dictionary<string, List<PromptAction>> _actions = [];
    private string _projectSearch = "", _selected = "", _keyword = "", _status = "", _priority = "", _assignee = "", _source = "", _error = "", _toast = "", _lastRefresh = "—";
    private bool _busy, _list;
    private int _page = 1, _size = 20, _loadVersion;
    private Project? Project => Workspace.Projects.FirstOrDefault(p => p.Id == _selected);
    /// <summary>收起更多筛选后仍提示已启用的条件数量，日期区间按一项计算。</summary>
    private bool _moreFilters;
    private int ActiveFilterCount => (string.IsNullOrEmpty(_priority) ? 0 : 1) + (string.IsNullOrEmpty(_assignee) ? 0 : 1) + (string.IsNullOrEmpty(_source) ? 0 : 1) + (_dateFrom.HasValue || _dateTo.HasValue ? 1 : 0);
    private DateTime? _dateFrom, _dateTo;
    private LocalDateRange _dateRange = new(null, null);
    private string _dateError = "";
    private IEnumerable<Bug> MatchingOtherFilters => _dateError.Length > 0 ? [] : _bugs.Where(b => _dateRange.Contains(b.CreateTime) && BugDisplay.Matches(b, _keyword, "", _priority, _assignee, _source));
    private List<Bug> Filtered => MatchingOtherFilters.Where(b => _status.Length == 0 || b.Status == _status).ToList();
    private IReadOnlyDictionary<string, int> StatusCounts => MatchingOtherFilters.GroupBy(b => b.Status).ToDictionary(g => g.Key, g => g.Count());
    /// <summary>标签切换同步作用于看板和列表，其他筛选及项目不变。</summary>
    private void SelectStatus(string status) { _status = status; ResetPage(); }
    /// <summary>用户选择本地日期，转 UTC 后与同步数据比较，保留其他筛选。</summary>
    private void ApplyDates()
    {
        try { _dateRange = LocalDateRange.Create(_dateFrom, _dateTo); _dateError = ""; }
        catch (ArgumentException ex) { _dateError = ex.Message; }
        ResetPage();
    }
    private void ClearDates() { _dateFrom = null; _dateTo = null; ApplyDates(); }
    /// <summary>打开一次完整项目集合。</summary>
    protected override async Task OnInitializedAsync() { Host.Changed += OnChanged; await Reload(); }
    private void OnChanged() => _ = InvokeAsync(Reload);
    /// <summary>同项目刷新保留滚动；切换项目后同时提交数据并重建滚动容器。</summary>
    private async Task Reload()
    {
        var version = ++_loadVersion; _busy = true; _error = "";
        try
        {
            await Workspace.LoadAsync();
            if (version != _loadVersion) return;
            var requested = Host.RequestedProjectId ?? View.ProjectId;
            var selected = !string.IsNullOrEmpty(requested) && Workspace.Projects.Any(p => p.Id == requested) ? requested : _selected;
            if (!Workspace.Projects.Any(p => p.Id == selected)) selected = Workspace.Projects.FirstOrDefault()?.Id ?? "";
            var bugs = selected == "" ? [] : await Api.BugsAsync(selected);
            var actions = new Dictionary<string, List<PromptAction>>();
            foreach (var status in bugs.Select(b => b.Status).Distinct()) actions[status] = await Api.ActionsAsync(status);
            if (version != _loadVersion) return;
            // 项目标识和结果一起更新，过期请求不能把新项目的界面切回去。
            if (_selected != selected) _page = 1;
            _selected = selected;
            _bugs = bugs; _actions = actions; _lastRefresh = DateTimeOffset.UtcNow.ToLocalTime().ToString("HH:mm");
            _page = Math.Clamp(_page, 1, Math.Max(1, (int)Math.Ceiling((double)Filtered.Count / _size)));
        }
        catch (Exception ex) { if (version == _loadVersion) _error = AppView.SafeError(ex); }
        finally { if (version == _loadVersion) { _busy = false; StateHasChanged(); } }
    }
    private Task SelectProject(string id) => Host.OpenAsync(new(DesktopViewKind.Main, ProjectId: id), View);
    private Task OpenBug(Bug bug) => Host.OpenAsync(new(DesktopViewKind.Detail, bug.Id, bug.ProjectId), View);
    private Task OpenProject() => Project is null ? Task.CompletedTask : Host.OpenAsync(new(DesktopViewKind.Project, Project.Id, Project.Id), View);
    private Task OnCardKey(KeyboardEventArgs e, Bug bug) => e.Key == "Enter" ? OpenBug(bug) : Task.CompletedTask;
    private void ResetPage() => _page = 1;
    private Task OpenAccount() => Host.OpenExternalAsync(Options.Value.HeimdallWebBaseUrl.TrimEnd('/') + "/account");
    private static PromptAction Primary(List<PromptAction> actions) => actions.FirstOrDefault(a => a.IsPrimary) ?? actions[0];
    private string OwnerTitle(Bug b) => ClientText.T("负责人：") + Workspace.Assignee(b.AssigneeId) + ClientText.T("\n标识：") + (b.AssigneeId ?? ClientText.T("未指派"));
    private string ClaimTitle(Bug b) => ClientText.T("领取者：") + BugDisplay.Claimant(b, Workspace.Users) + ClientText.T("\n标识：") + b.LockOwnerUserId + "\nWorker ID：" + b.LockOwnerId + ClientText.T("\n到期：") + b.LockExpireTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    private async Task Copy(string value) { await Host.CopyAsync(value); _toast = ClientText.T("已复制"); }
    private Dictionary<string, string?> Variables(Bug? bug = null) => new()
    { ["projectId"] = Project?.Id, ["projectName"] = Project?.Name, ["projectCode"] = Project?.Code, ["bugId"] = bug?.Id, ["bugTitle"] = bug?.Title, ["mcpEndpoint"] = Options.Value.ApiBaseUrl.TrimEnd('/') + "/api/mcp" };
    private async Task CopyPrompt(PromptAction action, Bug bug)
    { try { await Copy(await Api.PromptAsync(action.Key, Variables(bug))); } catch (Exception ex) { _error = AppView.SafeError(ex); } }
    private async Task BatchPrompt()
    { try { await Copy(await Api.PromptAsync("projectBatchFixPrompt", Variables())); } catch (Exception ex) { _error = AppView.SafeError(ex); } }
    private async Task DeleteBug(Bug bug)
    {
        if (!await Host.ConfirmAsync(ClientText.T("确认删除 BUG？"), bug.Title)) return;
        if (!await Host.ConfirmAsync(ClientText.T("再次确认物理删除？"), ClientText.T("会同时删除 BUG、评论、状态历史和附件，此操作不可恢复。"))) return;
        try { await Api.DeleteBugAsync(bug.Id); Host.PublishChange(); } catch (Exception ex) { _error = AppView.SafeError(ex); }
    }
    private async Task Logout() { try { await Session.LogoutAsync(); } catch (Exception ex) { _error = AppView.SafeError(ex); } }
    /// <summary>窗口销毁后解除事件，防止关闭窗口继续请求。</summary>
    public void Dispose() { _loadVersion++; Host.Changed -= OnChanged; }
}
