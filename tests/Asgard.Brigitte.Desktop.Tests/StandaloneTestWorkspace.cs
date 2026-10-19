using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Microsoft.Extensions.Options;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>专用临时本机库和拒绝所有 HTTP 的边界，用于本机业务集成测试。</summary>
internal sealed class StandaloneTestWorkspace : IDisposable
{
    public string DirectoryPath { get; } = Directory.CreateTempSubdirectory("brigitte-local-test-").FullName;
    public StandaloneWorkspace Database { get; }
    public LocalWorkspaceDatabase OnlineDatabase { get; }
    public SessionService Session { get; }
    public BrigitteApi Api { get; }
    public LocalWorkspaceReader Reader { get; }
    public MemoryCredentialStore Credentials { get; } = new() { Value = "untouched-online-credentials" };
    public TestWorkspaceSync Sync { get; } = new() { CanWrite = false };
    public TestTransport Transport { get; } = new(_ => throw new InvalidOperationException("本机模式不得发送 HTTP"));

    /// <summary>构造不会打开数据库或恢复账号，模拟每次进程启动的欢迎页状态。</summary>
    public StandaloneTestWorkspace()
    {
        Database = new(Path.Combine(DirectoryPath, "local"));
        OnlineDatabase = new(Path.Combine(DirectoryPath, "online"));
        Session = new(Options.Create(new ClientOptions()), Transport, Credentials, Credentials);
        Reader = new(OnlineDatabase, Database, Session);
        Api = new(Transport, Session, Reader, Sync, Database);
    }

    /// <summary>复用产品入口顺序，打开本机库成功后才允许进入工作区。</summary>
    public async Task EnterAsync() { Api.OpenLocalWorkspace(); await Session.UseLocalAsync(TestContext.Current.CancellationToken); }
    /// <summary>业务样本不含真实账号或项目数据。</summary>
    public Task<Project> ProjectAsync() => Api.WriteAsync<Project>("projects", new ProjectInput { Name = "本机项目", Description = "专用测试" }, cancellationToken: TestContext.Current.CancellationToken);
    /// <summary>测试结束关闭连接，并确认临时目录边界后清除测试数据。</summary>
    public void Dispose()
    {
        Database.Dispose(); OnlineDatabase.Dispose(); Session.Dispose(); Transport.Dispose();
        var path = Path.GetFullPath(DirectoryPath);
        if (path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("brigitte-local-test-", StringComparison.Ordinal)) Directory.Delete(path, true);
    }
}
