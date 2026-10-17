using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Asgard.Brigitte.Desktop;

/// <summary>同一 Windows 用户和桌面会话只运行一个客户端，发布目录不参与实例标识。</summary>
internal sealed class WindowsSingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();
    private readonly NamedPipeServerStream? _server;
    private Action? _activate;
    private bool _pending, _disposed;
    internal bool IsPrimary { get; }

    /// <summary>系统互斥锁在进程结束后自动释放；异常退出遗留的所有权可安全接管。</summary>
    internal WindowsSingleInstance()
    {
        using var identity = WindowsIdentity.GetCurrent();
        using var process = Process.GetCurrentProcess();
        _pipeName = $"Asgard.Brigitte.Desktop.{identity.User!.Value}.{process.SessionId}";
        _mutex = new Mutex(false, @"Local\" + _pipeName);
        try { IsPrimary = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
        if (!IsPrimary) return;
        // 先创建监听端点，避免首个窗口尚未出现时丢失重复启动请求。
        _server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _ = ListenAsync(_server, _stopping.Token);
    }

    /// <summary>主窗口可显示后绑定 UI 调度；启动期间的请求合并为一次激活。</summary>
    internal void BindActivation(Action activate)
    {
        bool pending;
        lock (_gate)
        {
            if (_disposed) return;
            _activate = activate;
            pending = _pending;
            _pending = false;
        }
        if (pending) activate();
    }

    /// <summary>只通知已有实例，并把本次用户启动授予的前台权限交给它。</summary>
    internal async Task ActivateExistingAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync(timeout.Token);
            if (GetNamedPipeServerProcessId(client.SafePipeHandle, out var processId)) AllowSetForegroundWindow(processId);
            await client.WriteAsync(new byte[] { 1 }, timeout.Token);
            var acknowledgement = new byte[1];
            await client.ReadExactlyAsync(acknowledgement, timeout.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            // 已有进程正退出或暂时不可响应时仍不创建第二个客户端。
            Debug.WriteLine($"Brigitte 激活已有实例未完成：{ex.GetType().Name}");
        }
    }

    /// <summary>本机同用户管道只传递单字节激活指令，不接收路径或业务操作。</summary>
    private async Task ListenAsync(NamedPipeServerStream server, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await server.WaitForConnectionAsync(stopping);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var command = new byte[1];
                await server.ReadExactlyAsync(command, timeout.Token);
                if (command[0] == 1) RequestActivation();
                await server.WriteAsync(command, timeout.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                if (stopping.IsCancellationRequested) return;
            }
            finally
            {
                // 重用同一管道句柄，快速连点时其他启动进程可等待下一次连接。
                try { if (!stopping.IsCancellationRequested && server.IsConnected) server.Disconnect(); }
                catch (ObjectDisposedException) when (stopping.IsCancellationRequested) { /* 正常退出与断开连接并发。 */ }
            }
        }
    }

    /// <summary>接收线程不操作窗口；准备完成后由绑定的 UI 调度器激活。</summary>
    private void RequestActivation()
    {
        Action? activate;
        lock (_gate)
        {
            if (_disposed) return;
            activate = _activate;
            if (activate is null) _pending = true;
        }
        activate?.Invoke();
    }

    /// <summary>首个进程在原 STA 线程释放锁，关闭监听后才允许下一次正常启动。</summary>
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; _activate = null; }
        _stopping.Cancel();
        _server?.Dispose();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _stopping.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
