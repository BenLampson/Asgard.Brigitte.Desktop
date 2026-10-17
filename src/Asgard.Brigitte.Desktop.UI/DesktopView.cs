namespace Asgard.Brigitte.Desktop.UI;
/// <summary>原生窗口的非敏感导航上下文。</summary>
public sealed record DesktopView(DesktopViewKind Kind, string? Id = null, string? ProjectId = null);
