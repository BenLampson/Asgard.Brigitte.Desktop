using Microsoft.JSInterop;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>静态组件渲染不访问真实浏览器 JS。</summary>
internal sealed class RenderJsRuntime : IJSRuntime, IJSObjectReference, IDisposable
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Dispose() { }
}
