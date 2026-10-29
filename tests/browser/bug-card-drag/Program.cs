using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Desktop.UI;
using BugCardDragPreview;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:8766");
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var data = Directory.CreateTempSubdirectory("brigitte-card-drag-preview-").FullName;
var host = new PreviewHost();
builder.Services.AddSingleton(host);
builder.Services.AddSingleton<IClientHost>(host);
builder.Services.AddSingleton<ICredentialStore>(host);
builder.Services.AddSingleton<ILoginBrowser>(host);
builder.Services.AddSingleton<IHttpClientFactory>(host);
builder.Services.AddSingleton<IWorkspaceSync>(host);
builder.Services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions { ApiBaseUrl = "https://brigitte.test/", Authority = "https://identity.test/tenant", HeimdallWebBaseUrl = "https://identity.test/" }));
builder.Services.AddSingleton(new StandaloneWorkspace(Path.Combine(data, "local")));
builder.Services.AddSingleton(new LocalWorkspaceDatabase(Path.Combine(data, "online")));
builder.Services.AddSingleton(new BoardPreferenceStore(Path.Combine(data, "board.json")));
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<IClientSession>(p => p.GetRequiredService<SessionService>());
builder.Services.AddSingleton<LocalWorkspaceReader>();
builder.Services.AddSingleton<BrigitteApi>();
builder.Services.AddSingleton<WorkspaceStore>();
var app = builder.Build();
var api = app.Services.GetRequiredService<BrigitteApi>();
api.OpenLocalWorkspace();
await app.Services.GetRequiredService<SessionService>().UseLocalAsync();
ClientText.SetLanguage("zh-CN");
var project = await api.WriteAsync<Project>("projects", new ProjectInput { Name = "卡片拖动验证", Code = "card-drag-test" });
foreach (var (title, status, worker) in new[] {
    ("可直接拖动的问题", "waiting", ""), ("正在处理的问题", "processing", ""),
    ("等待人工回归的问题", "pending_regression", ""), ("其他人正在处理的问题", "waiting", "another-worker"),
    ("我已手工领取的问题", "waiting", host.WorkerId) })
{
    var bug = await api.CreateBugAsync(new() { ProjectId = project.Id, Title = title, Priority = "medium" });
    if (status != "waiting")
    {
        if (status == "pending_regression") await api.StatusAsync(bug.Id, "pending_deployment", "虚构测试环境 v1：准备部署", null);
        await api.StatusAsync(bug.Id, status, "虚构测试环境 v1：已部署", null);
    }
    if (worker != "") await api.ClaimAsync(bug.Id, worker);
}
var first = new[] { "waiting", "processing", "pending_deployment", "pending_regression", "human_verified", "closed" };
app.Services.GetRequiredService<BoardPreferenceStore>().Set("local-workspace", project.Id, first.Concat(BugDisplay.BoardStatuses.Except(first)).ToArray());
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.GetFullPath("../../../src/Asgard.Brigitte.Desktop.UI/wwwroot")), RequestPath = "/assets" });
app.UseAntiforgery(); app.MapStaticAssets();
app.MapGet("/test/state", async () => new { bugs = await api.BugsAsync(project.Id), host.Opened, host.Copies, host.Confirmations, host.CanWrite });
app.MapRazorComponents<Preview>().AddInteractiveServerRenderMode();
app.Run();
