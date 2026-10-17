using System.Reflection;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>仅在测试渲染中选择列表页，不为产品添加测试开关或改变用户默认视图。</summary>
internal sealed class ListRenderActivator : IComponentActivator
{
    /// <summary>保留真实组件和注入流程，只设置与点击“列表”相同的显示状态。</summary>
    public IComponent CreateInstance(Type componentType)
    {
        var component = (IComponent)Activator.CreateInstance(componentType)!;
        if (component is WorkspaceView)
            typeof(WorkspaceView).GetField("_list", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, true);
        return component;
    }
}
