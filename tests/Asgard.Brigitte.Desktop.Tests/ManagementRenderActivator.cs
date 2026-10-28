using System.Reflection;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>仅测试渲染时选择 BUG 管理视图。</summary>
internal sealed class ManagementRenderActivator : IComponentActivator
{
    public IComponent CreateInstance(Type componentType)
    {
        var component = (IComponent)Activator.CreateInstance(componentType)!;
        if (component is WorkspaceView)
            typeof(WorkspaceView).GetField("_manage", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, true);
        return component;
    }
}
