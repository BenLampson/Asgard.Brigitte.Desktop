using Asgard.Brigitte.Desktop.UI.Components;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Sync;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>退役状态不再提供选项，旧数据仍可在待补充信息中找到。</summary>
public sealed class LegacyLaneVisibilityTests
{
    [Theory]
    [InlineData("fixed", 0, false)]
    [InlineData("regressed", 0, false)]
    [InlineData("fixed", 1, false)]
    [InlineData("regressed", 1, false)]
    [InlineData("fixed", 0, true)]
    [InlineData("regressed", 0, true)]
    public async Task Retired_lanes_and_filters_are_never_selectable(string status, int count, bool selected)
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
        Assert.DoesNotContain("status-filter-" + status, filter);
        Assert.DoesNotContain("data-lane=\"" + status + "\"", board);
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("regressed")]
    public void Legacy_bug_stays_visible_without_rewriting_the_original(string status)
    {
        using var local = new SyncTestWorkspace();
        local.Replace(SyncProtocol.Row("bug", "legacy", new Bug { Id = "legacy", ProjectId = "p", Status = status }));
        Assert.Equal("need_info", Assert.Single(new LocalWorkspaceReader(local.Database).Bugs()).Status);
        Assert.Equal(status, Assert.Single(local.Database.Read<Bug>("bug")).Status);
        Assert.DoesNotContain(status, BugDisplay.BoardStatuses);
    }
}
