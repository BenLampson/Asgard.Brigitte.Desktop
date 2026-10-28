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
    internal Task SwitchAsync(bool list) => SwitchAsync(list ? 1 : 0);
    internal async Task SwitchAsync(int mode)
    {
        var frames = GetCurrentRenderTreeFrames(_root);
        var events = new List<ulong>();
        for (var i = 0; i < frames.Count; i++)
            if (frames.Array[i].FrameType == RenderTreeFrameType.Attribute && frames.Array[i].AttributeName == "class" && (string?)frames.Array[i].AttributeValue == "segmented")
                for (var j = i + 1; j < frames.Count && events.Count < 3; j++)
                    if (frames.Array[j].FrameType == RenderTreeFrameType.Attribute && frames.Array[j].AttributeName == "onclick") events.Add(frames.Array[j].AttributeEventHandlerId);
        InsertedClasses.Clear();
        await DispatchEventAsync(events[mode], null, new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }
    internal string Text()
    {
        var frames = GetCurrentRenderTreeFrames(_root);
        return string.Concat(Enumerable.Range(0, frames.Count).Where(i => frames.Array[i].FrameType == RenderTreeFrameType.Text)
            .Select(i => frames.Array[i].TextContent));
    }
    internal async Task ClickButtonAsync(string label)
    {
        var frames = GetCurrentRenderTreeFrames(_root);
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Element || frames.Array[i].ElementName != "button") continue;
            var end = i + frames.Array[i].ElementSubtreeLength;
            var text = string.Concat(Enumerable.Range(i + 1, end - i - 1).Select(j => frames.Array[j].FrameType switch
            {
                RenderTreeFrameType.Text => frames.Array[j].TextContent,
                RenderTreeFrameType.Markup => System.Net.WebUtility.HtmlDecode(frames.Array[j].MarkupContent),
                _ => ""
            }));
            if (text != label) continue;
            for (var j = i + 1; j < end && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                if (frames.Array[j].AttributeName == "onclick")
                {
                    await DispatchEventAsync(frames.Array[j].AttributeEventHandlerId, null, new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
                    return;
                }
        }
        throw new InvalidOperationException("找不到按钮：" + label);
    }
    internal async Task ChangeCheckboxAsync(string label, bool value)
    {
        var frames = GetCurrentRenderTreeFrames(_root);
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Element || frames.Array[i].ElementName != "input") continue;
            var attributes = new List<int>();
            for (var j = i + 1; j < frames.Count && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++) attributes.Add(j);
            if (!attributes.Any(j => frames.Array[j].AttributeName == "aria-label" && (string?)frames.Array[j].AttributeValue == label)) continue;
            var change = attributes.First(j => frames.Array[j].AttributeName == "onchange");
            await DispatchEventAsync(frames.Array[change].AttributeEventHandlerId, null, new ChangeEventArgs { Value = value });
            return;
        }
        throw new InvalidOperationException("找不到选择框：" + label);
    }
}
#pragma warning restore BL0006
