using System.Reflection;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>切换语言通过级联重绘原编辑器，不清空尚未保存的 BUG 草稿。</summary>
public sealed class LanguageDraftTests
{
    [Fact]
    public async Task Switching_language_preserves_the_live_editor_and_its_draft()
    {
        using var transport = new TestTransport(_ => throw new InvalidOperationException("语言切换不得访问 REST。"));
        var credentials = new RenderCredentialStore();
        using var session = new SessionService(Options.Create(new ClientOptions()), transport, credentials, credentials);
        using var workspace = new SyncTestWorkspace();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, RenderJsRuntime>();
        services.AddSingleton(session); services.AddSingleton<IClientSession>(session);
        services.AddSingleton<IHttpClientFactory>(transport); services.AddSingleton(workspace.Database);
        services.AddSingleton<LocalWorkspaceReader>(); services.AddSingleton<IWorkspaceSync>(new TestWorkspaceSync());
        services.AddSingleton<BrigitteApi>(); services.AddSingleton<WorkspaceStore>();
        services.AddSingleton<IClientHost>(new RenderHost("light"));
        var activator = new LanguageComponentActivator(); services.AddSingleton<IComponentActivator>(activator);
        using var provider = services.BuildServiceProvider();
        await using var renderer = new LanguageTestRenderer(provider);
        RenderFragment editor = builder =>
        {
            builder.OpenComponent<BugEditorView>(0);
            builder.AddAttribute(1, nameof(BugEditorView.View), new DesktopView(DesktopViewKind.Create, ProjectId: "project"));
            builder.CloseComponent();
        };
        ParameterView Parameters(string language) => ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(CascadingValue<string>.Value)] = language,
            [nameof(CascadingValue<string>.Name)] = "DesktopLanguage",
            [nameof(CascadingValue<string>.ChildContent)] = editor
        });
        var id = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            using var language = ClientText.UseLanguage("en");
            return await renderer.MountAsync(new CascadingValue<string>(), Parameters("en"));
        });
        var instance = Assert.Single(activator.Editors);
        var input = (BugInput)typeof(BugEditorView).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
        input.Title = "Unsaved draft / 未保存草稿";
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            using var language = ClientText.UseLanguage("zh-CN");
            await renderer.UpdateAsync(id, Parameters("zh-CN"));
            Assert.Contains("提交 BUG", renderer.Text(id));
            Assert.DoesNotContain("Report bug", renderer.Text(id));
        });
        Assert.Same(instance, Assert.Single(activator.Editors));
        Assert.Equal("Unsaved draft / 未保存草稿", input.Title);
    }
}
