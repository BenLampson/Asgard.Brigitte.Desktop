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
        services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json"))); services.AddSingleton<IClientHost>(new RenderHost("light"));
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

    [Fact]
    public async Task Management_shows_only_closed_bugs()
    {
        using var local = new StandaloneTestWorkspace();
        await local.EnterAsync(); var project = await local.ProjectAsync();
        await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "尚未关闭的问题" });
        var closed = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "已经关闭的问题" });
        foreach (var status in new[] { "pending_deployment", "pending_regression", "human_verified", "closed" })
            await local.Api.StatusAsync(closed.Id, status, "测试", null);
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(local.Api); services.AddSingleton(local.Session);
        services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton<IClientHost>(new RenderHost("light")); services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        using var provider = services.BuildServiceProvider(); await using var renderer = new WorkspaceSwitchRenderer(provider);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync(); await renderer.SwitchAsync(2);
            Assert.Contains("table-scroll", renderer.InsertedClasses);
            Assert.Contains("已经关闭的问题", renderer.Text());
            Assert.DoesNotContain("尚未关闭的问题", renderer.Text());
            Assert.Contains("备份并批量删除", renderer.Text());
        });
    }

    [Fact]
    public async Task Canceling_save_does_not_delete_or_ask_for_final_confirmation()
    {
        using var local = new StandaloneTestWorkspace();
        await local.EnterAsync(); var project = await local.ProjectAsync();
        var closed = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "待备份的问题" });
        foreach (var status in new[] { "pending_deployment", "pending_regression", "human_verified", "closed" })
            await local.Api.StatusAsync(closed.Id, status, "测试", null);
        var host = new RenderHost("light") { ConfirmResult = true, SaveResult = false };
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(local.Api); services.AddSingleton(local.Session);
        services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton<IClientHost>(host); services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        using var provider = services.BuildServiceProvider(); await using var renderer = new WorkspaceSwitchRenderer(provider);
        await renderer.Dispatcher.InvokeAsync(async () => { await renderer.MountAsync(); await renderer.SwitchAsync(2); await renderer.ClickButtonAsync("全选当前筛选"); await renderer.ClickButtonAsync("备份并批量删除"); });
        Assert.Equal(1, host.ConfirmationCount); Assert.Equal(1, host.SaveCount);
        Assert.Single(await local.Api.BugsAsync(project.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Selection_is_retained_across_pages_and_select_all_covers_all_filtered_bugs()
    {
        using var local = new StandaloneTestWorkspace(); await local.EnterAsync(); var project = await local.ProjectAsync();
        for (var i = 0; i < 22; i++)
        {
            var bug = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = $"批量项-{i:00}" });
            foreach (var status in new[] { "pending_deployment", "pending_regression", "human_verified", "closed" })
                await local.Api.StatusAsync(bug.Id, status, "测试", null);
        }
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(local.Api); services.AddSingleton(local.Session);
        services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton<IClientHost>(new RenderHost("light")); services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        using var provider = services.BuildServiceProvider(); await using var renderer = new WorkspaceSwitchRenderer(provider);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync(); await renderer.SwitchAsync(2);
            await renderer.ChangeCheckboxAsync("选择 BUG：批量项-21", true);
            await renderer.ClickButtonAsync("›");
            await renderer.ChangeCheckboxAsync("选择 BUG：批量项-00", true);
            Assert.Contains("已选择 2 条", renderer.Text());
            await renderer.ClickButtonAsync("全选当前筛选");
            Assert.Contains("已选择 22 条", renderer.Text());
            await renderer.ClickButtonAsync("清空选择");
            Assert.Contains("已选择 0 条", renderer.Text());
        });
    }

    [Fact]
    public async Task Selected_closed_bugs_are_backed_up_and_deleted_together()
    {
        using var local = new StandaloneTestWorkspace(); await local.EnterAsync(); var project = await local.ProjectAsync();
        for (var i = 0; i < 2; i++)
        {
            var bug = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = $"已关闭-{i}" });
            foreach (var status in new[] { "pending_deployment", "pending_regression", "human_verified", "closed" })
                await local.Api.StatusAsync(bug.Id, status, "测试", null);
        }
        var open = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "保留未关闭" });
        var host = new RenderHost("light") { ConfirmResult = true, SaveResult = true };
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(local.Api); services.AddSingleton(local.Session);
        services.AddSingleton<WorkspaceStore>(); services.AddSingleton(new BoardPreferenceStore(Path.Combine(Path.GetTempPath(), "brigitte-board-render-" + Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton<IClientHost>(host); services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        using var provider = services.BuildServiceProvider(); await using var renderer = new WorkspaceSwitchRenderer(provider);
        await renderer.Dispatcher.InvokeAsync(async () => { await renderer.MountAsync(); await renderer.SwitchAsync(2); await renderer.ClickButtonAsync("全选当前筛选"); await renderer.ClickButtonAsync("备份并批量删除"); });
        Assert.Equal(2, host.ConfirmationCount); Assert.Equal(1, host.SaveCount); Assert.NotNull(host.SavedContent);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(host.SavedContent), System.IO.Compression.ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("bugs.sqlite")); Assert.NotNull(zip.GetEntry("bugs.xlsx"));
        var remaining = Assert.Single(await local.Api.BugsAsync(project.Id, TestContext.Current.CancellationToken));
        Assert.Equal(open.Id, remaining.Id);
    }
}
