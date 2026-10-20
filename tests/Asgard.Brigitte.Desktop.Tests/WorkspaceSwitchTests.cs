using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>真实点击反复切换列表与看板，验证布局容器重建，不触发网络请求。</summary>
public sealed class WorkspaceSwitchTests
{
    [Fact]
    public async Task Switching_views_recreates_container_with_correct_layout_class()
    {
        using var local = new StandaloneTestWorkspace();
        await local.EnterAsync(); await local.ProjectAsync();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(local.Api); services.AddSingleton(local.Session);
        services.AddSingleton<WorkspaceStore>(); services.AddSingleton<IClientHost>(new RenderHost("light"));
        services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        using var provider = services.BuildServiceProvider();
        await using var renderer = new WorkspaceSwitchRenderer(provider);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync();
            for (var i = 0; i < 3; i++)
            {
                await renderer.SwitchAsync(true); Assert.Contains("table-scroll", renderer.InsertedClasses);
                await renderer.SwitchAsync(false); Assert.Contains("lanes", renderer.InsertedClasses);
            }
        });
        Assert.Equal(0, local.Transport.Count);
    }
}
