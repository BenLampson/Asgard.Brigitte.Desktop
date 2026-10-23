using Asgard.Brigitte.Desktop.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>迁移后隐藏空历史列，同时避免旧本机数据或已选筛选条件被藏掉。</summary>
public sealed class LegacyLaneVisibilityTests
{
    [Theory]
    [InlineData("fixed", 0, false)]
    [InlineData("regressed", 0, false)]
    [InlineData("fixed", 1, false)]
    [InlineData("regressed", 1, false)]
    [InlineData("fixed", 0, true)]
    [InlineData("regressed", 0, true)]
    public async Task Legacy_lane_and_filter_are_visible_only_with_records_or_explicit_selection(string status, int count, bool selected)
    {
        using var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, RenderJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var parameters = new Dictionary<string, object?>
        {
            ["Counts"] = new Dictionary<string, int> { [status] = count },
            ["Selected"] = selected ? new HashSet<string> { status } : new HashSet<string>()
        };
        var filter = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<StatusFilters>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        parameters["ChildContent"] = (RenderFragment<string>)(_ => _ => { });
        var board = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<LaneOrder>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        Assert.Equal(count > 0 || selected, filter.Contains("status-filter-" + status, StringComparison.Ordinal));
        Assert.Equal(count > 0 || selected, board.Contains("data-lane=\"" + status + "\"", StringComparison.Ordinal));
    }
}
