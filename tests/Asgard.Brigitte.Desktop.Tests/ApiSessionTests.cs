using System.Net;
using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.Core.Models;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>认证失效、并发响应和分页是桌面客户端的主要接口边界。</summary>
public sealed class ApiSessionTests
{
    [Fact]
    public async Task Unauthenticated_does_not_send_business_request()
    {
        var session = new TestSession { Authenticated = false }; using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{}")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TestWorkspaceSync.Api(transport, session).CreateBugAsync(new BugInput())); Assert.Equal(0, transport.Count);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(419)]
    [InlineData(440)]
    public async Task Expired_write_invalidates_once_and_never_replays(int status)
    {
        var session = new TestSession(); using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{}", (HttpStatusCode)status)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TestWorkspaceSync.Api(transport, session).CreateBugAsync(new BugInput())); Assert.Equal(1, transport.Count); Assert.Equal(1, session.Invalidations);
    }
    [Fact]
    public async Task Late_unauthorized_cannot_clear_new_login()
    {
        var session = new TestSession(); using var transport = new TestTransport(_ => { session.Version = 2; return Task.FromResult(TestTransport.Json("{}", HttpStatusCode.Unauthorized)); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TestWorkspaceSync.Api(transport, session).CreateBugAsync(new BugInput())); Assert.True(session.Authenticated); Assert.Equal(0, session.Invalidations);
    }
    [Fact]
    public async Task Permission_denied_does_not_log_out()
    {
        var session = new TestSession(); using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{\"code\":403,\"message\":\"无权限\"}", HttpStatusCode.Forbidden)));
        await Assert.ThrowsAsync<HttpRequestException>(() => TestWorkspaceSync.Api(transport, session).CreateBugAsync(new BugInput())); Assert.True(session.Authenticated); Assert.Equal(1, transport.Count);
    }
    [Fact]
    public async Task Rejects_redirect_instead_of_forwarding_credentials()
    {
        using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{}", HttpStatusCode.Redirect))); await Assert.ThrowsAsync<HttpRequestException>(() => TestWorkspaceSync.Api(transport, new TestSession()).CreateBugAsync(new BugInput())); Assert.Equal(1, transport.Count);
    }
    [Fact]
    public async Task Unzoned_server_dates_are_UTC()
    {
        using var transport = new TestTransport(_ => Task.FromResult(TestTransport.Json("{\"code\":200,\"data\":{\"id\":\"b\",\"createTime\":\"2026-10-16T10:30:00\"}}"))); var bug = await TestWorkspaceSync.Api(transport, new TestSession()).CreateBugAsync(new BugInput()); Assert.Equal(TimeSpan.Zero, bug.CreateTime.Offset);
    }
}
