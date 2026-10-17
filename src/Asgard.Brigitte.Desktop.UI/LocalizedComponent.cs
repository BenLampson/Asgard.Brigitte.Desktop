using Microsoft.AspNetCore.Components;

namespace Asgard.Brigitte.Desktop.UI;

/// <summary>语言级联变化仅重绘现有组件，不重建表单或丢弃编辑草稿。</summary>
public abstract class LocalizedComponent : ComponentBase
{
    [CascadingParameter(Name = "DesktopLanguage")] public string? DesktopLanguage { get; set; }
}
