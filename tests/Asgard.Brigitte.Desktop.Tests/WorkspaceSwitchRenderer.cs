using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using Asgard.Brigitte.Desktop.UI;
namespace Asgard.Brigitte.Desktop.Tests;

#pragma warning disable BL0006 // 检查真实渲染差异，确保模式切换重建正确的 DOM 容器。
/// <summary>记录 Blazor 实际插入的元素，防止仅验证最终静态 HTML 遗漏切换问题。</summary>
internal sealed class WorkspaceSwitchRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    internal List<string> InsertedClasses { get; } = [];
    private int _root;
    protected override void HandleException(Exception exception) => throw exception;
    protected override Task UpdateDisplayAsync(in RenderBatch batch)
    {
        for (var i = 0; i < batch.UpdatedComponents.Count; i++)
        {
            var edits = batch.UpdatedComponents.Array[i].Edits;
            for (var j = 0; j < edits.Count; j++)
            {
                var edit = edits[j];
                if (edit.Type != RenderTreeEditType.PrependFrame) continue;
                var frames = batch.ReferenceFrames;
                var index = edit.ReferenceFrameIndex;
                if (frames.Array[index].FrameType != RenderTreeFrameType.Element) continue;
                for (var k = index + 1; k < frames.Count && frames.Array[k].FrameType == RenderTreeFrameType.Attribute; k++)
                    if (frames.Array[k].AttributeName == "class") InsertedClasses.Add(frames.Array[k].AttributeValue?.ToString() ?? "");
            }
        }
        return Task.CompletedTask;
    }
    internal async Task MountAsync()
    {
        _root = AssignRootComponentId(InstantiateComponent(typeof(WorkspaceView)));
        await RenderRootComponentAsync(_root);
    }
    internal async Task SwitchAsync(bool list)
    {
        var frames = GetCurrentRenderTreeFrames(_root);
        var events = new List<ulong>();
        for (var i = 0; i < frames.Count; i++)
            if (frames.Array[i].FrameType == RenderTreeFrameType.Attribute && frames.Array[i].AttributeName == "class" && (string?)frames.Array[i].AttributeValue == "segmented")
                for (var j = i + 1; j < frames.Count && events.Count < 2; j++)
                    if (frames.Array[j].FrameType == RenderTreeFrameType.Attribute && frames.Array[j].AttributeName == "onclick") events.Add(frames.Array[j].AttributeEventHandlerId);
        InsertedClasses.Clear();
        await DispatchEventAsync(events[list ? 1 : 0], null, new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }
}
#pragma warning restore BL0006
