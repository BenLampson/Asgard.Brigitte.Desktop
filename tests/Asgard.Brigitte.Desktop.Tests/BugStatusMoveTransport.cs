using System.Net;
using System.Text.Json;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
using Asgard.Brigitte.Sync;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>独立的虚构远端及真实本地同步库，检查命令顺序、身份和锁归属。</summary>
internal sealed class BugStatusMoveTransport : IDisposable
{
    private readonly SyncTestWorkspace _workspace = new();
    private readonly TestTransport _transport;
    internal Bug Server { get; } = new() { Id = "b", ProjectId = "p", Status = "processing" };
    internal TestSession Session { get; } = new();
    internal TestWorkspaceSync Sync { get; } = new();
    internal List<(string Action, JsonElement Body)> Requests { get; } = [];
    internal string? Reject { get; set; }
    internal string? StatusAtClaim { get; set; }
    internal Action? AfterClaim { get; set; }
    internal BrigitteApi Api { get; }

    internal BugStatusMoveTransport()
    {
        _transport = new TestTransport(async request =>
        {
            var action = request.RequestUri!.Segments.Last();
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync());
            Requests.Add((action, body));
            if (Reject == action) return TestTransport.Json("{\"code\":409,\"message\":\"conflict\"}", HttpStatusCode.Conflict);
            var worker = body.GetProperty("workerId").GetString();
            if (action == "claim")
            {
                Server.LockOwnerId = worker; Server.LockOwnerUserId = "u";
                Server.LockExpireTime = DateTimeOffset.UtcNow.AddMinutes(body.GetProperty("ttlMinutes").GetInt32());
                if (StatusAtClaim is not null) Server.Status = StatusAtClaim;
                AfterClaim?.Invoke();
            }
            if (action == "status")
            {
                Assert.Equal(Server.LockOwnerId, worker);
                Assert.Equal("human", body.GetProperty("operatorType").GetString());
                Server.Status = body.GetProperty("status").GetString()!;
            }
            if (action == "release-lock")
            {
                Assert.Equal(Server.LockOwnerId, worker);
                Server.LockOwnerId = Server.LockOwnerUserId = null; Server.LockExpireTime = null;
            }
            Publish();
            return TestTransport.Json(JsonSerializer.Serialize(new { code = 200, data = Server }));
        });
        Publish();
        Api = new(_transport, Session, new(_workspace.Database), Sync);
    }

    internal void Publish() => _workspace.Replace(SyncProtocol.Row("bug", "b", Server));
    internal Task<BugStatusMoveResult> Move(string status = "confirmed", string reason = "")
        => Api.MoveStatusAsync("b", "processing", status, reason, "desktop-worker", "u");
    public void Dispose() { _transport.Dispose(); _workspace.Dispose(); }
}
