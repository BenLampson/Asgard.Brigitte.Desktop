using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>只捕获实际编辑器实例，属性注入和生命周期仍由 Blazor 执行。</summary>
internal sealed class LanguageComponentActivator : IComponentActivator
{
    public List<BugEditorView> Editors { get; } = [];
    public IComponent CreateInstance(Type type)
    {
        var component = (IComponent)Activator.CreateInstance(type)!;
        if (component is BugEditorView editor) Editors.Add(editor);
        return component;
    }
}
