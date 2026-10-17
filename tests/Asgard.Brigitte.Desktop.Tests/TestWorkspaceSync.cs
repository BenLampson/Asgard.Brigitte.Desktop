using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>命令测试控制同步屏障，默认立即确认以隔离 HTTP 认证测试。</summary>
internal sealed class TestWorkspaceSync : IWorkspaceSync
{
    public bool CanWrite { get; set; } = true;
    public TaskCompletionSource? Barrier { get; set; }
    public int Confirmations { get; private set; }
    public Task ConfirmAsync(CancellationToken token = default) { Confirmations++; return Barrier?.Task.WaitAsync(token) ?? Task.CompletedTask; }
    public static BrigitteApi Api(IHttpClientFactory factory, IClientSession session, IWorkspaceSync? sync = null)
        => new(factory, session, new LocalWorkspaceReader(new LocalWorkspaceDatabase(Path.GetTempPath())), sync ?? new TestWorkspaceSync());
}
