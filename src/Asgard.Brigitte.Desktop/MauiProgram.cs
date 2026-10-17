using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop;
/// <summary>原生宿主装配，业务与凭据不进入 JavaScript 存储。</summary>
public static class MauiProgram
{
    /// <summary>为 Windows 与 Mac Catalyst 创建同一套业务服务。</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Asgard.Brigitte.Desktop.Resources.Raw.clientsettings.json")!;
        var options = JsonSerializer.Deserialize<ClientOptions>(stream)!;
        if (!options.IsValid()) throw new InvalidOperationException("客户端服务地址配置无效。");
        builder.Services.AddSingleton<IOptions<ClientOptions>>(Options.Create(options));
        builder.Services.AddBrigitteDesktopHttp(options);
        builder.Services.AddSingleton<ICredentialStore, SecureCredentialStore>();
        builder.Services.AddSingleton<ILoginBrowser>(_ => CreateLoginBrowser());
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<SessionMaintenance>();
        builder.Services.AddSingleton<DesktopRealtime>();
        builder.Services.AddSingleton(_ => new LocalWorkspaceDatabase(Path.Combine(FileSystem.AppDataDirectory, "workspace")));
        builder.Services.AddSingleton<LocalWorkspaceReader>();
        builder.Services.AddSingleton<IWorkspaceSync>(sp => sp.GetRequiredService<DesktopRealtime>());
        builder.Services.AddSingleton<IClientSession>(sp => sp.GetRequiredService<SessionService>());
        builder.Services.AddSingleton<BrigitteApi>();
        builder.Services.AddSingleton<WorkspaceStore>();
        builder.Services.AddSingleton(_ => new ThemePreferenceStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Asgard", "Brigitte", "theme.json"),
            () => Preferences.Default.Get("brigitte.theme", "system")));
        builder.Services.AddSingleton<DesktopHost>();
        builder.Services.AddSingleton(_ => new ClosePreferenceStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Asgard", "Brigitte", "close-behavior.json")));
        builder.Services.AddSingleton<IClientHost>(sp => sp.GetRequiredService<DesktopHost>());
        return builder.Build();
    }
    /// <summary>系统启动器和统一 SVG 留在原生层，回调传输可独立验证。</summary>
    private static LoopbackLoginBrowser CreateLoginBrowser()
    {
        using var logo = typeof(MauiProgram).Assembly.GetManifestResourceStream("Asgard.Brigitte.Desktop.Resources.AppIcon.appicon.svg")!;
        using var reader = new StreamReader(logo);
        return new LoopbackLoginBrowser(uri => Launcher.Default.OpenAsync(uri), reader.ReadToEnd());
    }
}
