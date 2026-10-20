using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>状态入口保留零计数并准确切换，选中状态可再次点击取消。</summary>
public sealed class StatusFiltersTests
{
    [Fact]
    public async Task All_statuses_and_counts_remain_visible()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using (services)
        await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
        {
            var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StatusFilters>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Counts"] = new Dictionary<string, int> { ["fixed"] = 7, ["processing"] = 3 }, ["Selected"] = "fixed" }))).ToHtmlString());
            var decoded = System.Net.WebUtility.HtmlDecode(html);
            foreach (var label in BugDisplay.Statuses.Values) Assert.Contains(label, decoded);
            Assert.Contains(">10</span>", decoded);
            Assert.Contains(">7</span>", decoded);
            Assert.Contains(">0</span>", decoded);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "aria-pressed=\"true\""));
        }
    }
    [Theory]
    [InlineData("", "fixed")]
    [InlineData("fixed", "")]
    public async Task Clicking_a_status_selects_or_clears_it(string current, string expected)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new PromptTestRenderer(services);
        string? selected = null;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var events = await renderer.MountAsync(new StatusFilters(), ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Selected"] = current, ["SelectedChanged"] = EventCallback.Factory.Create<string>(this, value => selected = value) }));
            await renderer.ClickAsync(events[1 + Array.IndexOf(BugDisplay.BoardStatuses, "fixed")]);
        });
        Assert.Equal(expected, selected);
    }
}
