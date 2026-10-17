using Microsoft.AspNetCore.Components;
using Asgard.Brigitte.Desktop.Core.Models;
namespace Asgard.Brigitte.Desktop.UI;
/// <summary>现有项目、身份目录和 MCP Key 能力的独立窗口。</summary>
public partial class ManagementView
{
    /// <summary>宿主窗口上下文。</summary>
    [Parameter] public DesktopView View { get; set; } = new(DesktopViewKind.Projects);
    private Project? _project;
    private ProjectInput _input = new() { Status = "active" };
    private readonly HashSet<string> _members = [];
    private readonly HashSet<string> _scopes = ["bug:read", "bug:write"];
    private List<McpKey> _keys = [];
    private List<Bug> _projectBugs = [];
    private CreatedMcpKey? _result;
    private BugAttachment? _image;
    private string _error = "", _keyName = "", _setupPrompt = "";
    private bool _busy;
    private int _page = 1;
    private string Title => View.Kind switch { DesktopViewKind.Projects => ClientText.T("我的项目"), DesktopViewKind.Project => ClientText.T("项目详情"), DesktopViewKind.ProjectEdit => ClientText.T("编辑项目"), DesktopViewKind.ProjectCreate => ClientText.T("创建项目"), DesktopViewKind.Keys => "MCP Key", DesktopViewKind.KeyCreate => ClientText.T("创建 MCP Key"), DesktopViewKind.KeyResult => ClientText.T("保存 MCP Key"), DesktopViewKind.Users => ClientText.T("用户目录"), _ => ClientText.T("附件预览") };
    private string ImageSource => _image is null ? "" : "data:" + _image.ContentType + ";base64," + _image.Base64Content;
    /// <summary>项目负责人和租户管理员可编辑；成员仅有协作访问权。</summary>
    private bool CanManage(Project? project) => project is not null
        && (Workspace.Access.IsTenantAdmin || project.OwnerId == Workspace.Access.UserId);
    /// <summary>一次性结果不随刷新重复消费。</summary>
    protected override async Task OnInitializedAsync()
    {
        if (View.Kind == DesktopViewKind.KeyResult) { _result = KeyResultStore.Take(View.Id ?? ""); if (_result is not null) await BuildSetupPrompt(); return; }
        if (View.Kind == DesktopViewKind.Image) { _image = ImagePreviewStore.Take(View.Id ?? ""); return; }
        await Load();
        if (View.Kind is DesktopViewKind.Projects or DesktopViewKind.Project or DesktopViewKind.Keys or DesktopViewKind.Users) Host.Changed += Changed;
    }
    private void Changed() => _ = InvokeAsync(Load);
    private async Task Load()
    {
        try
        {
            await Workspace.LoadAsync();
            if (View.Kind is DesktopViewKind.Project or DesktopViewKind.ProjectEdit)
            {
                _project = await Api.GetAsync<Project>("projects/" + Uri.EscapeDataString(View.Id ?? ""));
                if (View.Kind == DesktopViewKind.Project) _projectBugs = await Api.BugsAsync(_project.Id);
                else { _input = new() { Name = _project.Name, Code = _project.Code, Description = _project.Description, OwnerId = _project.OwnerId, Status = _project.Status }; _members.UnionWith(_project.MemberUserIds); }
            }
            if (View.Kind == DesktopViewKind.Keys) _keys = await Api.GetAsync<List<McpKey>>("mcp-api-keys");
        }
        catch (Exception ex) { _project = null; _projectBugs = []; _keys = []; _error = AppView.SafeError(ex); }
        StateHasChanged();
    }
    private void Member(string id, bool value) { if (value) _members.Add(id); else _members.Remove(id); }
    private void Scope(string id, bool value) { if (value) _scopes.Add(id); else _scopes.Remove(id); }
    private void Close() => Host.Close(View);
    private Task ManageUsers() => Host.OpenExternalAsync(Options.Value.HeimdallWebBaseUrl.TrimEnd('/') + "/tenant/" + Uri.EscapeDataString(Options.Value.TenantId) + "/admin/users");
    private async Task Run(Func<Task> action) { _busy = true; _error = ""; try { await action(); } catch (Exception ex) { _error = AppView.SafeError(ex); } finally { _busy = false; } }
    private Task SaveProject() => Run(async () =>
    {
        if (View.Kind != DesktopViewKind.ProjectCreate && !CanManage(_project)) throw new InvalidOperationException(ClientText.T("没有项目管理权限。"));
        if (string.IsNullOrWhiteSpace(_input.Name)) throw new InvalidOperationException(ClientText.T("请填写项目名称。"));
        _input.MemberUserIds = _members.ToArray();
        await Api.WriteAsync<Project>(View.Kind == DesktopViewKind.ProjectCreate ? "projects" : "projects/" + Uri.EscapeDataString(View.Id ?? ""), _input, View.Kind == DesktopViewKind.ProjectCreate ? "POST" : "PUT");
        Host.PublishChange(); Close();
    });
    private Task CreateKey() => Run(async () =>
    {
        if (string.IsNullOrWhiteSpace(_keyName) || _scopes.Count == 0) throw new InvalidOperationException(ClientText.T("请填写名称并选择权限范围。"));
        var value = await Api.WriteAsync<CreatedMcpKey>("mcp-api-keys", new { name = _keyName, scopes = _scopes.ToArray() });
        var id = KeyResultStore.Put(value); await Host.OpenAsync(new(DesktopViewKind.KeyResult, id), View); Host.PublishChange(); Close();
    });
    private Task Revoke(McpKey key) => Run(async () =>
    {
        if (!await Host.ConfirmAsync(ClientText.T("撤销 MCP Key？"), ClientText.T("使用“{0}”的客户端将立即无法访问 Brigitte。", key.Name))) return;
        await Api.WriteAsync<object>("mcp-api-keys/" + Uri.EscapeDataString(key.Id), null, "DELETE"); Host.PublishChange();
    });
    private Task BatchPrompt() => Run(async () =>
    {
        if (_project is null) return;
        await Host.CopyAsync(await Api.PromptAsync("projectBatchFixPrompt", new() { ["projectId"] = _project.Id, ["projectName"] = _project.Name, ["projectCode"] = _project.Code, ["mcpEndpoint"] = Options.Value.ApiBaseUrl.TrimEnd('/') + "/api/mcp" }));
    });
    private async Task BuildSetupPrompt()
    {
        var endpoint = Options.Value.ApiBaseUrl.TrimEnd('/') + "/api/mcp";
        try { _setupPrompt = await Api.PromptAsync("mcpClientSetupPrompt", new() { ["mcpEndpoint"] = endpoint, ["mcpApiKey"] = _result!.PlainKey }); } catch { }
        if (string.IsNullOrWhiteSpace(_setupPrompt)) _setupPrompt = ClientText.T("请帮我把 Brigitte MCP 接入当前使用的 MCP Client。\n\n服务地址：") + endpoint + "\nAPI Key：" + _result!.PlainKey + ClientText.T("\n\n配置示例：\n") + System.Text.Json.JsonSerializer.Serialize(new { mcpServers = new { brigitte = new { url = endpoint, headers = new Dictionary<string, string> { ["X-Api-Key"] = _result.PlainKey } } } }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + ClientText.T("\n请完成配置后验证工具能够正常列出，不要在日志、代码仓库或聊天中泄露这个 Key。");
    }
    /// <summary>关闭后不保留密钥或预览图片。</summary>
    public void Dispose() { Host.Changed -= Changed; _result = null; _setupPrompt = ""; _image = null; }
}
