using System.Reflection;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Asgard.Brigitte.Sync;
using Asgard.Heimdall.Client;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SettingsPreview;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:8768");
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var data = Directory.CreateTempSubdirectory("brigitte-settings-preview-").FullName;
var preferences = new LocalPreferenceDatabase(Path.Combine(data, "preferences.sqlite"));
var host = new SettingsHost(new NotificationPreferenceStore(preferences, Path.Combine(data, "notifications.json")));
builder.Services.AddSingleton(host);
builder.Services.AddSingleton<IClientHost>(host);
builder.Services.AddSingleton<ICredentialStore>(host);
builder.Services.AddSingleton<ILoginBrowser>(host);
builder.Services.AddSingleton<IHttpClientFactory>(host);
builder.Services.AddSingleton<IWorkspaceSync>(host);
builder.Services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions { ApiBaseUrl = "https://example.test/", TenantId = "preview" }));
var database = new LocalWorkspaceDatabase(Path.Combine(data, "workspace"));
database.OpenAccount("preview");
var rows = new[] {
    SyncProtocol.Row("project", "p", new { id = "p", name = "示例项目", code = "BRG", status = "active" }),
    SyncProtocol.Row("project", "q", new { id = "q", name = "示例接口", code = "API", status = "active" }),
    SyncProtocol.Row("access", "current", new { })
};
var frame = new SyncFrame { Phase = "begin", BatchId = "preview", BaseCursor = SyncProtocol.Cursor(database.Manifest()), Replay = true,
    Cursor = SyncProtocol.Cursor(rows.Select(x => new SyncStamp(x.Key, x.Hash))), Total = rows.Length };
database.Apply(frame); database.Apply(frame with { Phase = "rows", Rows = rows }); database.Apply(frame with { Phase = "commit" });
builder.Services.AddSingleton(database);
builder.Services.AddSingleton(new BoardPreferenceStore(preferences, Path.Combine(data, "board.json")));
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<IClientSession>(services => services.GetRequiredService<SessionService>());
builder.Services.AddSingleton<LocalWorkspaceReader>();
builder.Services.AddSingleton<BrigitteApi>();
builder.Services.AddSingleton<WorkspaceStore>();
var app = builder.Build();
var session = app.Services.GetRequiredService<SessionService>();
typeof(SessionService).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
    new StoredSession { Tokens = new HeimdallTokenSet { AccessToken = "preview-only", ExpiresIn = 3600 }, SubjectId = "preview-user", SessionId = "preview", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
ClientText.SetLanguage("zh-CN");
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.GetFullPath("../../../src/Asgard.Brigitte.Desktop.UI/wwwroot")), RequestPath = "/assets" });
app.UseAntiforgery(); app.MapStaticAssets();
app.MapRazorComponents<Preview>().AddInteractiveServerRenderMode();
app.Run();
