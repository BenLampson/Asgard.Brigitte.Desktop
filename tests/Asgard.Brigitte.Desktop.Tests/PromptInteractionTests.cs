using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Desktop.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>提示词入口始终先选再复制，即使服务器只返回一项也不直接执行。</summary>
public sealed class PromptInteractionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Menu_includes_primary_and_copies_only_the_selected_template(int count)
    {
        var actions = new[]
        {
            new PromptAction { Key = "close", ActionLabel = "关闭", IsPrimary = true, SortOrder = 30 },
            new PromptAction { Key = "reopen", ActionLabel = "重新打开", SortOrder = 20 },
            new PromptAction { Key = "fix", ActionLabel = "复制修复提示词", SortOrder = 10 }
        }.Take(count).ToArray();
        var copied = new List<PromptAction>();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PromptButtons.Actions)] = actions,
            [nameof(PromptButtons.OnCopy)] = EventCallback.Factory.Create<PromptAction>(copied, (PromptAction action) => copied.Add(action))
        });
        var services = new ServiceCollection(); services.AddLogging();
        using var provider = services.BuildServiceProvider();
        await using var renderer = new PromptTestRenderer(provider);
        var buttons = await renderer.Dispatcher.InvokeAsync(() => renderer.MountAsync(new PromptButtons(), parameters));
        Assert.Equal(count + 1, buttons.Length);
        Assert.Equal(0UL, buttons[0]); Assert.Empty(copied);
        await renderer.Dispatcher.InvokeAsync(() => renderer.ClickAsync(buttons[1]));
        Assert.Same(actions.OrderBy(a => a.SortOrder).First(), Assert.Single(copied));

        await using var htmlRenderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () => (await htmlRenderer.RenderComponentAsync<PromptButtons>(parameters)).ToHtmlString());
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("复制关闭提示词", decoded);
        Assert.DoesNotContain("复制复制", decoded);
        Assert.DoesNotContain("提示词提示词", decoded);
        await WindowRenderTests.ExportPreview("prompt-" + count, "<div class='client dark' style='padding:24px'>" + html + "</div>");
    }
}
