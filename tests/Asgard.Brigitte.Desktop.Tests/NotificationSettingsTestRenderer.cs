using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using Asgard.Brigitte.Desktop.UI.Components;

namespace Asgard.Brigitte.Desktop.Tests;

#pragma warning disable BL0006 // 测试派发实际 Razor 表单事件，不进入产品构建。
/// <summary>按设置控件 ID 派发事件，验证真实绑定、保存和重新渲染，而非直接调用私有实现。</summary>
internal sealed class NotificationSettingsTestRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
{
    private int _componentId;
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    protected override void HandleException(Exception exception) => throw exception;
    protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;

    /// <summary>通过框架激活组件以注入原生能力、会话和工作区。</summary>
    internal async Task MountAsync()
    {
        _componentId = AssignRootComponentId(InstantiateComponent(typeof(NotificationPreferences)));
        await RenderRootComponentAsync(_componentId, ParameterView.Empty);
    }

    /// <summary>状态筛选等无依赖组件也通过真实渲染事件验证。</summary>
    internal async Task MountAsync(IComponent component, ParameterView parameters)
    {
        _componentId = AssignRootComponentId(component);
        await RenderRootComponentAsync(_componentId, parameters);
    }

    /// <summary>取当前渲染结果的处理器标识，避免复用重绘前已经失效的事件。</summary>
    internal Task ChangeAsync(string id, object value)
    {
        var attributes = Attributes(id);
        var handler = attributes.Single(x => x.AttributeName is "onchange" or "oninput").AttributeEventHandlerId;
        return DispatchEventAsync(handler, null, new ChangeEventArgs { Value = value });
    }

    /// <summary>检查失败恢复、继承切换之后界面实际显示的保存值。</summary>
    internal bool IsChecked(string id) => Attributes(id).Any(x => x.AttributeName == "checked" && x.AttributeValue is true);
    private RenderTreeFrame[] Attributes(string id)
    {
        var frames = GetCurrentRenderTreeFrames(_componentId);
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Element) continue;
            var attributes = new List<RenderTreeFrame>();
            for (var j = i + 1; j < frames.Count && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++) attributes.Add(frames.Array[j]);
            if (attributes.Any(x => x.AttributeName == "id" && Equals(x.AttributeValue, id))) return attributes.ToArray();
        }
        throw new InvalidOperationException("设置控件未渲染：" + id);
    }
}
#pragma warning restore BL0006
