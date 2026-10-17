using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
namespace Asgard.Brigitte.Desktop.Tests;

#pragma warning disable BL0006 // 测试用渲染器直接检查并派发真实 Razor 事件，不进入产品构建。
/// <summary>捕获提示词按钮的真实事件，验证只有选中菜单项才复制。</summary>
internal sealed class PromptTestRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    protected override void HandleException(Exception exception) => throw exception;
    protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;

    /// <summary>返回各按钮的 onclick 标识；纯下拉入口没有复制事件。</summary>
    internal async Task<ulong[]> MountAsync(IComponent component, ParameterView parameters)
    {
        var id = AssignRootComponentId(component);
        await RenderRootComponentAsync(id, parameters);
        var frames = GetCurrentRenderTreeFrames(id);
        var events = new List<ulong>();
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Element || frames.Array[i].ElementName != "button") continue;
            ulong handler = 0;
            for (var j = i + 1; j < frames.Count && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                if (frames.Array[j].AttributeName == "onclick") handler = frames.Array[j].AttributeEventHandlerId;
            events.Add(handler);
        }
        return events.ToArray();
    }

    /// <summary>调用 Blazor 的事件分发路径，保留 EventCallback 和组件更新语义。</summary>
    internal Task ClickAsync(ulong handler) => DispatchEventAsync(handler, null, EventArgs.Empty);
}
#pragma warning restore BL0006
