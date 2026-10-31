namespace Asgard.Brigitte.Desktop.Core;
/// <summary>业务读取来自 SQLite；命令与附件下载走线上，拒绝重定向、过期响应和写请求重放。</summary>
public sealed partial class BrigitteApi(IHttpClientFactory factory, IClientSession session, LocalWorkspaceReader local, IWorkspaceSync sync, StandaloneWorkspace? standalone = null)
{
    /// <summary>供界面隐藏仅适用于远程协作的能力。</summary>
    public bool IsLocal => session.IsLocal;
    private StandaloneWorkspace Standalone => standalone ?? throw new InvalidOperationException(ClientText.T("本机工作区尚未打开。"));
    /// <summary>打开本机事实库，不读取登录信息或建立同步连接。</summary>
    public void OpenLocalWorkspace() => Standalone.Open();
    private static readonly JsonSerializerOptions Json = CreateJson();
    /// <summary>项目和记录标识只作为编码后的路径段。</summary>
    private static string E(string value) => Uri.EscapeDataString(value);
    /// <summary>统一契约大小写及 UTC 时间。</summary>
    private static JsonSerializerOptions CreateJson()
    {
        var value = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        value.Converters.Add(new UtcDateTimeConverter());
        return value;
    }
    /// <summary>查询参数只从调用方明确提供的字段构建。</summary>
    public static string Query(params (string Key, object? Value)[] values)
        => string.Join("&", values.Where(x => x.Value is not null && !string.IsNullOrWhiteSpace(x.Value.ToString()))
            .Select(x => $"{E(x.Key)}={E(x.Value!.ToString()!)}"));
    /// <summary>保留统一响应的业务失败语义。</summary>
    private async Task<T> RequestAsync<T>(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default)
    {
        if (IsLocal) throw new InvalidOperationException(ClientText.T("此功能仅在在线模式可用。"));
        var lease = await session.GetLeaseAsync(cancellationToken);
        using var request = new HttpRequestMessage(method, "api/brigitte/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lease.AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var client = factory.CreateClient("brigitte");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (session.IdentityVersion != lease.IdentityVersion) throw new OperationCanceledException(ClientText.T("会话已变化，已丢弃旧响应。"));
        if ((int)response.StatusCode is 401 or 419 or 440)
        {
            await session.InvalidateAsync(lease.Version);
            throw new UnauthorizedAccessException(ClientText.T("登录已失效，请重新登录。"));
        }
        if ((int)response.StatusCode is >= 300 and < 400) throw new HttpRequestException(ClientText.T("API 不允许重定向。"));
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (session.IdentityVersion != lease.IdentityVersion) throw new OperationCanceledException(ClientText.T("会话已变化，已丢弃旧响应。"));
        var root = payload.RootElement;
        if (!response.IsSuccessStatusCode || !root.TryGetProperty("code", out var code) || code.GetInt32() != 200)
            throw new HttpRequestException(root.TryGetProperty("message", out var message) ? message.GetString() : $"HTTP {(int)response.StatusCode}", null, response.StatusCode);
        return root.Deserialize<T>(Json) ?? throw new JsonException(ClientText.T("API 返回空响应。"));
    }
    /// <summary>读取非分页数据。</summary>
    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        => await Task.FromResult(local.Get<T>(path));
    /// <summary>邀请是当前会话内的私有元数据，按需在线读取，绝不存入工作区缓存。</summary>
    public async Task<T> InvitationRequestAsync<T>(string path, object? body = null, CancellationToken cancellationToken = default)
    {
        var list = path.StartsWith("projects/", StringComparison.Ordinal) && path.EndsWith("/invitations", StringComparison.Ordinal) && path.Count(c => c == '/') == 2;
        if (path != "invitations/inspect" && !list)
            throw new ArgumentException(ClientText.T("不支持的邀请路径。"), nameof(path));
        return (await RequestAsync<ApiResponse<T>>(body is null ? HttpMethod.Get : HttpMethod.Post, path, body, cancellationToken)).Data;
    }
    /// <summary>执行已有写接口，绝不自动重试。</summary>
    public async Task<T> WriteAsync<T>(string path, object? body, string method = "POST", CancellationToken cancellationToken = default)
    {
        if (IsLocal) return Standalone.Write<T>(path, body, method, cancellationToken);
        if (!sync.CanWrite) throw new InvalidOperationException(ClientText.T("当前工作区只读，连接并完成同步后才能提交。"));
        if (method is not ("POST" or "PUT" or "DELETE" or "PATCH")) throw new ArgumentException(ClientText.T("命令只允许写方法。"));
        var identity = session.IdentityVersion;
        var value = (await RequestAsync<ApiResponse<T>>(new HttpMethod(method), path, body, cancellationToken)).Data;
        await sync.ConfirmAsync(cancellationToken);
        if (identity != session.IdentityVersion) throw new OperationCanceledException(ClientText.T("会话已变化，已丢弃旧命令结果。"));
        return value;
    }
    /// <summary>读取已经同步的完整本地集合，不再执行 HTTP 分页。</summary>
    public Task<List<T>> AllAsync<T>(string path, CancellationToken cancellationToken = default) => Task.FromResult(local.All<T>(path));
    /// <summary>所有可访问项目。</summary>
    public Task<List<Project>> ProjectsAsync(CancellationToken token = default) => AllAsync<Project>("projects", token);
    /// <summary>项目中的完整 BUG 集合。</summary>
    public Task<List<Bug>> BugsAsync(string projectId, CancellationToken token = default) => AllAsync<Bug>("bugs?projectId=" + E(projectId), token);
    /// <summary>默认只读详情。</summary>
    public Task<BugDetail> DetailAsync(string id) => Task.FromResult(local.Detail(id));
    /// <summary>按服务器当前状态返回提示词动作。</summary>
    public Task<List<PromptAction>> ActionsAsync(string status) => Task.FromResult(local.Actions(status));
    /// <summary>只渲染文本，不执行提示词或改变状态。</summary>
    public Task<string> PromptAsync(string key, Dictionary<string, string?> variables) => Task.FromResult(IsLocal ? Standalone.RenderPrompt(key, variables) : local.Prompt(key, variables));
    /// <summary>仅用户预览或下载时取单个附件正文；不写入业务投影。</summary>
    public async Task<BugAttachment> DownloadAsync(string bugId, string id)
        => IsLocal ? Standalone.Download(bugId, id) : (await RequestAsync<ApiResponse<BugAttachment>>(HttpMethod.Get, "bugs/" + E(bugId) + "/attachments/" + E(id))).Data;
    /// <summary>新增 BUG。</summary>
    public Task<Bug> CreateBugAsync(BugInput input) => WriteAsync<Bug>("bugs", input);
    /// <summary>更新已有 BUG，服务器负责授权和处理锁检查。</summary>
    public Task<Bug> UpdateBugAsync(string id, BugInput input) => WriteAsync<Bug>("bugs/" + E(id), input, "PUT");
    /// <summary>更新已有状态。</summary>
    public Task<Bug> StatusAsync(string id, string status, string reason, string? workerId)
        => WriteAsync<Bug>("bugs/" + E(id) + "/status", new { status, reason, operatorType = "human", workerId });
    /// <summary>领取处理锁；手工领取默认一小时，快捷流转使用短租期。</summary>
    public Task<Bug> ClaimAsync(string id, string workerId, int ttlMinutes = 60)
        => WriteAsync<Bug>("bugs/" + E(id) + "/claim", new { workerId, workerName = "Asgard Brigitte Desktop", ttlMinutes });
    /// <summary>仅释放本客户端的锁。</summary>
    public Task<Bug> ReleaseAsync(string id, string workerId) => WriteAsync<Bug>("bugs/" + E(id) + "/release-lock", new { workerId });
    /// <summary>新增现有评论或处理记录。</summary>
    public Task<BugComment> CommentAsync(string id, string content, string commentType, string authorType, string? workerId, Asgard.Brigitte.Sync.BugProcessReport? report = null)
        => WriteAsync<BugComment>("bugs/" + E(id) + "/comments", new { content, commentType, authorType, workerId, report });
    /// <summary>保留旧客户端契约；服务端和本机工作区会拒绝未备份的删除。</summary>
    public Task<JsonElement> DeleteBugAsync(string id)
        => WriteAsync<JsonElement>("bugs/" + E(id) + "?" + Query(("hardDelete", true), ("confirmBugId", id)), null, "DELETE");
    /// <summary>获取一次性备份和确认凭据；只有已关闭的 BUG 才能准备。</summary>
    public async Task<BugArchive> PrepareBugArchiveAsync(string id)
    {
        if (IsLocal) return await Task.Run(() => Standalone.PrepareArchive(id));
        if (!sync.CanWrite) throw new InvalidOperationException(ClientText.T("当前工作区只读，连接并完成同步后才能提交。"));
        return (await RequestAsync<ApiResponse<BugArchive>>(HttpMethod.Post, "bugs/" + E(id) + "/archive/prepare")).Data;
    }
    /// <summary>用户保存备份后确认物理删除，不自动重试。</summary>
    public Task<JsonElement> DeleteArchivedBugAsync(string id, string confirmationToken)
        => WriteAsync<JsonElement>("bugs/" + E(id) + "/archive/delete", new { confirmationToken });
    /// <summary>一次性准备同一项目内所有所选 BUG 的备份。</summary>
    public async Task<BugArchive> PrepareBugBatchArchiveAsync(string projectId, IReadOnlyList<string> bugIds)
    {
        if (IsLocal) return await Task.Run(() => Standalone.PrepareBatchArchive(projectId, bugIds));
        if (!sync.CanWrite) throw new InvalidOperationException(ClientText.T("当前工作区只读，连接并完成同步后才能提交。"));
        return (await RequestAsync<ApiResponse<BugArchive>>(HttpMethod.Post, "bugs/archive/prepare", new { projectId, bugIds })).Data;
    }
    /// <summary>确认已保存整批备份后统一物理删除。</summary>
    public Task<JsonElement> DeleteArchivedBugsAsync(string confirmationToken)
        => WriteAsync<JsonElement>("bugs/archive/delete", new { confirmationToken });
    /// <summary>添加已有 base64 附件。</summary>
    public Task<BugAttachment> UploadAsync(string id, string fileName, string contentType, byte[] content, string? workerId)
        => WriteAsync<BugAttachment>("bugs/" + E(id) + "/attachments", new { fileName, contentType, base64Content = Convert.ToBase64String(content), workerId });
    /// <summary>删除单个附件。</summary>
    public Task<JsonElement> DeleteAttachmentAsync(string id, string attachmentId, string? workerId)
        => WriteAsync<JsonElement>("bugs/" + E(id) + "/attachments/" + E(attachmentId) + "?" + Query(("workerId", workerId)), null, "DELETE");
}
