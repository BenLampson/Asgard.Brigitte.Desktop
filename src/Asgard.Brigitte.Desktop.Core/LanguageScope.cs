namespace Asgard.Brigitte.Desktop.Core;

/// <summary>恢复调用方语言作用域，重复释放不影响后续作用域。</summary>
internal sealed class LanguageScope(Action restore) : IDisposable
{
    private Action? _restore = restore;
    public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
}
