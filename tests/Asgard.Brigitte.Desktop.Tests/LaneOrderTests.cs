using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证 JS 拖放入口的保存契约，筛选和取消操作不能丢失已有泳道。</summary>
public sealed class LaneOrderTests
{
    [Theory]
    [InlineData("waiting", "triaged", false, "confirmed,reproducing,waiting,triaged")]
    [InlineData("waiting", "triaged", true, "confirmed,reproducing,triaged,waiting")]
    [InlineData("triaged", "waiting", false, "triaged,waiting,confirmed,reproducing")]
    [InlineData("triaged", "waiting", true, "waiting,triaged,confirmed,reproducing")]
    public async Task Drop_inserts_at_target_and_preserves_input(string source, string target, bool after, string expected)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new PromptTestRenderer(services);
        var original = BugDisplay.BoardStatuses.Take(4).ToArray();
        IReadOnlyList<string>? changed = null;
        var component = new LaneOrder { JS = new RenderJsRuntime() };
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync(component, ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Order"] = original, ["ChildContent"] = (RenderFragment<string>)(_ => _ => { }), ["OrderChanged"] = EventCallback.Factory.Create<IReadOnlyList<string>>(this, value => changed = value) }));
            await component.MoveLane(source, target, after);
        });
        Assert.NotNull(changed);
        Assert.Equal(expected.Split(','), changed);
        Assert.Equal(BugDisplay.BoardStatuses.Take(4), original);
    }

    /// <summary>筛选状态后拖放仍提交完整顺序，隐藏的列保留在各自相对位置。</summary>
    [Fact]
    public async Task Filtered_drag_preserves_hidden_lanes()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new PromptTestRenderer(services);
        var original = BugDisplay.BoardStatuses.ToArray();
        IReadOnlyList<string>? changed = null;
        var component = new LaneOrder { JS = new RenderJsRuntime() };
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync(component, ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Order"] = original, ["Selected"] = new HashSet<string> { "waiting", "triaged" },
                ["ChildContent"] = (RenderFragment<string>)(_ => _ => { }),
                ["OrderChanged"] = EventCallback.Factory.Create<IReadOnlyList<string>>(this, value => changed = value)
            }));
            await component.MoveLane("waiting", "triaged", true);
        });
        Assert.NotNull(changed);
        Assert.Equal(new[] { "confirmed", "reproducing", "triaged", "waiting" }, changed.Take(4));
        Assert.Equal(original.Skip(4), changed.Skip(4));
        Assert.Equal(original.Length, changed.Distinct().Count());
    }

    /// <summary>原位放下、无变化、外部拖入和不可见落点都不会写入偏好。</summary>
    [Theory]
    [InlineData("waiting", "waiting", true)]
    [InlineData("waiting", "confirmed", false)]
    [InlineData("external", "waiting", false)]
    [InlineData("waiting", "external", false)]
    [InlineData("waiting", "triaged", true)]
    public async Task Invalid_or_unchanged_drop_does_not_save(string source, string target, bool after)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new PromptTestRenderer(services);
        var saves = 0;
        var component = new LaneOrder { JS = new RenderJsRuntime() };
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync(component, ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Selected"] = new HashSet<string> { "waiting", "confirmed" },
                ["ChildContent"] = (RenderFragment<string>)(_ => _ => { }),
                ["OrderChanged"] = EventCallback.Factory.Create<IReadOnlyList<string>>(this, _ => saves++)
            }));
            await component.MoveLane(source, target, after);
        });
        Assert.Equal(0, saves);
    }
}
