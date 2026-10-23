using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>真实复选框事件验证多选保留其他选项及取消选择。</summary>
public sealed class StatusFiltersTests
{
    [Fact]
    public async Task Current_statuses_and_counts_remain_visible_without_empty_legacy_filters()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StatusFilters>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Counts"] = new Dictionary<string, int> { ["pending_deployment"] = 7, ["processing"] = 3 },
            ["Selected"] = new HashSet<string> { "pending_deployment", "processing" }
        }))).ToHtmlString());
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        foreach (var label in BugDisplay.Statuses.Where(s => s.Key is not ("fixed" or "regressed")).Select(s => s.Value)) Assert.Contains(label, decoded);
        Assert.DoesNotContain("status-filter-fixed", html);
        Assert.DoesNotContain("status-filter-regressed", html);
        Assert.Contains(">10</span>", decoded);
        Assert.Contains(">7</span>", decoded);
        Assert.Contains(">0</span>", decoded);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, " checked(?:[ =>])").Count);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Toggling_a_status_preserves_other_selection(bool alreadySelected)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new NotificationSettingsTestRenderer(services);
        var original = new HashSet<string> { "processing" };
        if (alreadySelected) original.Add("pending_deployment");
        IReadOnlySet<string>? selected = null;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync(new StatusFilters(), ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Selected"] = original, ["SelectedChanged"] = EventCallback.Factory.Create<IReadOnlySet<string>>(this, value => selected = value) }));
            await renderer.ChangeAsync("status-filter-pending_deployment", !alreadySelected);
        });
        Assert.NotNull(selected);
        Assert.Contains("processing", selected);
        Assert.Equal(!alreadySelected, selected.Contains("pending_deployment"));
        Assert.Equal(alreadySelected, original.Contains("pending_deployment"));
    }
}
