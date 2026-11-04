using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asgard.Brigitte.Desktop.Tests;

#pragma warning disable BL0006 // 测试通过真实 Razor 事件验证预览交互。
/// <summary>派发详情页按钮事件并检查当前渲染内容。</summary>
internal sealed class AttachmentTestRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
{
    private int _componentId;
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    protected override void HandleException(Exception exception) => throw exception;
    protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;

    internal async Task MountAsync(DesktopView view)
    {
        _componentId = AssignRootComponentId(InstantiateComponent(typeof(BugDetailView)));
        await RenderRootComponentAsync(_componentId, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BugDetailView.View)] = view }));
    }

    internal int ButtonCount(string text) => Buttons().Count(button => button.Text == text);
    internal Task ClickAsync(string text) => DispatchEventAsync(Buttons().Single(button => button.Text == text).Handler, null, new MouseEventArgs());
    internal string RenderedText()
    {
        var frames = GetCurrentRenderTreeFrames(_componentId);
        return string.Concat(frames.Array.Take(frames.Count).Where(frame => frame.FrameType == RenderTreeFrameType.Text).Select(frame => frame.TextContent));
    }

    private List<(string Text, ulong Handler)> Buttons()
    {
        var frames = GetCurrentRenderTreeFrames(_componentId);
        var result = new List<(string, ulong)>();
        for (var i = 0; i < frames.Count; i++)
        {
            var element = frames.Array[i];
            if (element.FrameType != RenderTreeFrameType.Element || element.ElementName != "button") continue;
            ulong handler = 0;
            var text = "";
            for (var j = i + 1; j < i + element.ElementSubtreeLength; j++)
            {
                var frame = frames.Array[j];
                if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick") handler = frame.AttributeEventHandlerId;
                if (frame.FrameType == RenderTreeFrameType.Text) text += frame.TextContent;
            }
            result.Add((text, handler));
        }
        return result;
    }
}
#pragma warning restore BL0006
