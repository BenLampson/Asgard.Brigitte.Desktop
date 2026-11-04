using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>真实详情页的预览按钮只调用系统打开能力并显示失败信息。</summary>
public sealed class AttachmentPreviewInteractionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preview_click_passes_downloaded_image_to_host_or_displays_open_failure(bool fail)
    {
        using var language = ClientText.UseLanguage("zh-CN");
        using var local = new StandaloneTestWorkspace();
        await local.EnterAsync();
        var project = await local.ProjectAsync();
        var bug = await local.Api.CreateBugAsync(new() { ProjectId = project.Id, Title = "图片预览", Priority = "high" });
        byte[] content = [0, 1, 2, 255];
        await local.Api.UploadAsync(bug.Id, "截图.png", "image/png", content, null);
        await local.Api.UploadAsync(bug.Id, "普通文件.txt", "text/plain", [42], null);
        var host = new RenderHost("light") { AttachmentOpenError = fail ? new InvalidOperationException("系统打开失败") : null };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(local.Api);
        services.AddSingleton(local.Session);
        services.AddSingleton<WorkspaceStore>();
        services.AddSingleton<IClientHost>(host);
        services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new AttachmentTestRenderer(provider);
        await renderer.Dispatcher.InvokeAsync(() => renderer.MountAsync(new DesktopView(DesktopViewKind.Detail, bug.Id, project.Id)));
        await renderer.Dispatcher.InvokeAsync(() => renderer.ClickAsync("附件"));

        Assert.Equal(1, await renderer.Dispatcher.InvokeAsync(() => renderer.ButtonCount("预览")));
        await renderer.Dispatcher.InvokeAsync(() => renderer.ClickAsync("预览"));

        Assert.Equal(0, host.OpenedWindowCount);
        if (fail)
        {
            Assert.Contains("系统打开失败", await renderer.Dispatcher.InvokeAsync(renderer.RenderedText));
            Assert.Null(host.OpenedAttachmentContent);
        }
        else
        {
            Assert.Equal("截图.png", host.OpenedAttachmentName);
            Assert.Equal("image/png", host.OpenedAttachmentType);
            Assert.Equal(content, host.OpenedAttachmentContent);
        }
    }
}
