using System.Reflection;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Asgard.Brigitte.Sync;
using Asgard.Heimdall.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>从真实 Razor 事件验证“默认静默，只提醒关注项目”工作流和保存失败恢复。</summary>
public sealed class NotificationSettingsInteractionTests
{
    /// <summary>测试只使用临时 SQLite 和虚构身份，不进行网络访问。</summary>
    [Fact]
    public async Task Project_controls_save_status_rules_restore_inheritance_and_recover_from_write_failure()
    {
        using var local = new SyncTestWorkspace();
        local.Replace(SyncProtocol.Row("project", "p", new { id = "p", name = "关注项目" }),
            SyncProtocol.Row("access", "current", new { }));
        using var transport = new TestTransport(_ => throw new InvalidOperationException("设置不得访问网络。"));
        var credentials = new RenderCredentialStore();
        using var session = new SessionService(Options.Create(new ClientOptions()), transport, credentials, credentials);
        typeof(SessionService).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new StoredSession { Tokens = new HeimdallTokenSet { AccessToken = "test-only", ExpiresIn = 3600 }, SubjectId = "user" });
        var config = Path.Combine(local.DirectoryPath, "notifications.json");
        var preferences = new NotificationPreferenceStore(config);
        var host = new RenderHost("light") { NotificationPreferences = preferences };
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(session); services.AddSingleton<IClientSession>(session);
        services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton<IHttpClientFactory>(transport); services.AddSingleton(local.Database);
        services.AddSingleton<LocalWorkspaceReader>(); services.AddSingleton<IWorkspaceSync>(new TestWorkspaceSync());
        services.AddSingleton<BrigitteApi>(); services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json"))); services.AddSingleton<IClientHost>(host);
        using var provider = services.BuildServiceProvider();
        await using var renderer = new NotificationSettingsTestRenderer(provider);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync();
            await renderer.ChangeAsync("notification-enabled", false);
            Assert.False(preferences.Get("render-test").Enabled);
            await renderer.ChangeAsync("notification-project", "p");
            Assert.True(renderer.IsChecked("notification-inherit"));
            Assert.False(renderer.IsChecked("notification-enabled"));
            await renderer.ChangeAsync("notification-inherit", false);
            Assert.True(preferences.HasOverride("render-test", "p"));
            await renderer.ChangeAsync("notification-enabled", true);
            foreach (var type in Enum.GetValues<NotificationChange>().Where(x => x is not (NotificationChange.None or NotificationChange.All or NotificationChange.Status)))
                await renderer.ChangeAsync("notification-type-" + type, false);
            await renderer.ChangeAsync("notification-all-statuses", false);
            await renderer.ChangeAsync("notification-status-pending_regression", true);
            await renderer.ChangeAsync("notification-status-closed", true);
            Assert.Equal(NotificationChange.Status, preferences.Get("render-test", "p").Changes);
            Assert.Equal(new[] { "closed", "pending_regression" }, preferences.Get("render-test", "p").Statuses!.Order().ToArray());
            Assert.True(preferences.Get("render-test", "p").Enabled);
            Assert.False(preferences.Get("render-test", "other-project").Enabled);
            await renderer.ChangeAsync("notification-status-processing", true);
            await renderer.ChangeAsync("notification-status-processing", false);
            Assert.False(renderer.IsChecked("notification-status-processing"));
            Assert.True(renderer.IsChecked("notification-status-pending_regression"));
            await renderer.ChangeAsync("notification-inherit", true);
            Assert.False(preferences.HasOverride("render-test", "p"));
            Assert.False(renderer.IsChecked("notification-enabled"));
            await renderer.ChangeAsync("notification-project", "");
            Directory.CreateDirectory(config + ".tmp");
            await renderer.ChangeAsync("notification-enabled", true);
            Assert.False(preferences.Get("render-test").Enabled);
            Assert.False(renderer.IsChecked("notification-enabled"));
        });
    }
}
