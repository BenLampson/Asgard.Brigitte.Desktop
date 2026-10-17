using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asgard.Brigitte.Desktop.Tests;

#pragma warning disable BL0006 // 通过真实级联参数更新验证编辑器实例和草稿，不进入产品构建。
/// <summary>保留组件树并读取显示文本，允许测试语言切换时的现有组件更新。</summary>
internal sealed class LanguageTestRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    protected override void HandleException(Exception exception) => throw exception;
    protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
    internal async Task<int> MountAsync(IComponent component, ParameterView parameters)
    {
        var id = AssignRootComponentId(component);
        await RenderRootComponentAsync(id, parameters);
        return id;
    }
    internal Task UpdateAsync(int id, ParameterView parameters) => RenderRootComponentAsync(id, parameters);
    internal string Text(int id)
    {
        var text = new System.Text.StringBuilder();
        var frames = GetCurrentRenderTreeFrames(id);
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Text) text.Append(frame.TextContent);
            else if (frame.FrameType == RenderTreeFrameType.Markup) text.Append(frame.MarkupContent);
            else if (frame.FrameType == RenderTreeFrameType.Component) text.Append(Text(frame.ComponentId));
        }
        return text.ToString();
    }
}
#pragma warning restore BL0006
