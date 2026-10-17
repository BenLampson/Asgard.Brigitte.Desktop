using Microsoft.JSInterop;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>静态组件渲染不访问真实浏览器 JS。</summary>
internal sealed class RenderJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
}
