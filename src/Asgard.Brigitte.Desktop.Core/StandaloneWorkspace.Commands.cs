using Microsoft.Data.Sqlite;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本机命令只支持单机业务，不接受邀请、远程身份或 MCP Key 操作。</summary>
public sealed partial class StandaloneWorkspace
{
    /// <summary>保留界面的现有命令契约，在进程内执行，不开启网络端口。</summary>
    private object Dispatch(string[] path, string query, JsonElement input, string method, SqliteTransaction transaction)
    {
        if (path is ["projects"] && method == "POST") return SaveProject(null, input, transaction);
        if (path is ["projects", var projectId] && method == "PUT") return SaveProject(projectId, input, transaction);
        if (path is ["bugs"] && method == "POST") return SaveBug(null, input, transaction);
        if (path is ["bugs", var bugId])
        {
            if (method == "PUT") return SaveBug(bugId, input, transaction);
            if (method == "DELETE") return DeleteBug(bugId, query, transaction);
        }
        if (path is ["bugs", var id, var action] && method == "POST")
        {
            var bug = RequireBug(id);
            if (action == "release-lock") return Release(bug, input, transaction);
            if (action == "claim") return Claim(bug, input, transaction);
            RequireWritable(bug, Property(input, "workerId"));
            switch (action)
            {
                case "status": return ChangeStatus(bug, input, transaction);
                case "comments": return AddComment(bug, input, transaction);
                case "attachments": return AddAttachment(bug, input, transaction);
            }
        }
        if (path is ["bugs", var attachmentBug, "attachments", var attachmentId] && method == "DELETE")
        {
            RequireWritable(RequireBug(attachmentBug), Query(query, "workerId"));
            var attachment = Read<BugAttachment>("attachment").SingleOrDefault(x => x.Id == attachmentId && x.BugId == attachmentBug)
                ?? throw new InvalidOperationException(ClientText.T("附件不存在。"));
            DeleteAttachment(attachment.Id, transaction);
            return new { };
        }
        throw new InvalidOperationException(ClientText.T("此功能仅在在线模式可用。"));
    }

    /// <summary>项目归属固定为本机用户，更新先读取原记录以保留创建信息。</summary>
    private Project SaveProject(string? id, JsonElement input, SqliteTransaction transaction)
    {
        var value = input.Deserialize<ProjectInput>(Json) ?? throw new ArgumentException(ClientText.T("缺少项目内容。"));
        var project = id is null ? new Project { Id = NewId(), CreateTime = DateTimeOffset.UtcNow, OwnerId = UserId }
            : Read<Project>("project").SingleOrDefault(x => x.Id == id) ?? throw new InvalidOperationException(ClientText.T("项目不存在。"));
        if (value.OwnerId is { Length: > 0 } owner && owner != UserId || value.MemberUserIds.Any(x => x != UserId))
            throw new ArgumentException(ClientText.T("本机工作区只能选择本机用户。"));
        var status = string.IsNullOrWhiteSpace(value.Status) ? "active" : value.Status;
        if (status is not ("active" or "archived")) throw new ArgumentException(ClientText.T("项目状态无效。"));
        project.Name = Text(value.Name, 100, true);
        project.Code = Text(value.Code, 50);
        if (project.Code.Length == 0) project.Code = "local-" + project.Id[..8];
        if (Read<Project>("project").Any(x => x.Id != project.Id && x.Code.Equals(project.Code, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException(ClientText.T("项目编码已存在。"));
        project.Description = value.Description ?? "";
        project.Status = status;
        project.MemberUserIds = [UserId];
        if (id is not null) project.UpdateTime = DateTimeOffset.UtcNow;
        Save("project", project.Id, project, transaction);
        return project;
    }

    /// <summary>创建与编辑同用字段校验，编辑不会覆盖状态、锁或创建时间。</summary>
    private Bug SaveBug(string? id, JsonElement input, SqliteTransaction transaction)
    {
        var value = input.Deserialize<BugInput>(Json) ?? throw new ArgumentException(ClientText.T("缺少 BUG 内容。"));
        var bug = id is null ? new Bug { Id = NewId(), Status = "waiting", CreateTime = DateTimeOffset.UtcNow } : RequireBug(id);
        if (id is not null) RequireWritable(bug, value.WorkerId);
        var project = Read<Project>("project").SingleOrDefault(x => x.Id == value.ProjectId)
            ?? throw new ArgumentException(ClientText.T("项目不存在。"));
        if (id is not null && bug.ProjectId != project.Id) throw new ArgumentException(ClientText.T("不能通过编辑移动 BUG 所属项目。"));
        var priority = string.IsNullOrWhiteSpace(value.Priority) ? "medium" : value.Priority;
        if (!BugDisplay.Priorities.ContainsKey(priority)) throw new ArgumentException(ClientText.T("优先级无效。"));
        bug.ProjectId = project.Id;
        bug.Title = Text(value.Title, 200, true);
        bug.Description = value.Description ?? "";
        bug.Content = value.Content ?? "";
        bug.Priority = priority;
        bug.AssigneeId = Assignee(value.AssigneeId);
        bug.AffectedServices = value.AffectedServices.Select(x => Text(x, 200)).Where(x => x.Length > 0).Distinct().ToArray();
        bug.DeploymentNotes = value.DeploymentNotes ?? "";
        bug.Source = string.IsNullOrWhiteSpace(value.Source) ? "desktop" : Text(value.Source, 50);
        if (id is not null) bug.UpdateTime = DateTimeOffset.UtcNow;
        Save("bug", bug.Id, bug, transaction);
        return bug;
    }

    /// <summary>状态与历史一起提交；同状态不制造重复历史。</summary>
    private Bug ChangeStatus(Bug bug, JsonElement input, SqliteTransaction transaction)
    {
        var status = Property(input, "status");
        if (status is null || !BugDisplay.Statuses.ContainsKey(status)) throw new ArgumentException(ClientText.T("BUG 状态无效。"));
        BugWorkflow.Validate(bug.Status, status, Property(input, "reason"));
        if (bug.Status == status) return bug;
        var history = new BugHistory { Id = NewId(), BugId = bug.Id, FromStatus = bug.Status, ToStatus = status,
            Reason = Property(input, "reason") ?? "", OperatorType = "human", CreateTime = DateTimeOffset.UtcNow };
        bug.Status = status; bug.UpdateTime = history.CreateTime;
        Save("history", history.Id, history, transaction); Save("bug", bug.Id, bug, transaction);
        return bug;
    }

    /// <summary>本地回复只记录真实用户操作，不接受外部伪造的作者类型。</summary>
    private BugComment AddComment(Bug bug, JsonElement input, SqliteTransaction transaction)
    {
        var type = Property(input, "commentType") ?? "comment";
        if (type is not ("comment" or "process")) throw new ArgumentException(ClientText.T("记录类型无效。"));
        var comment = new BugComment { Id = NewId(), BugId = bug.Id, Content = Text(Property(input, "content"), 1_000_000, true),
            CommentType = type, AuthorType = "human", CreateTime = DateTimeOffset.UtcNow };
        Save("comment", comment.Id, comment, transaction);
        return comment;
    }

    /// <summary>保留客户端领取行为，处理锁与普通编辑共用同一数据库事务。</summary>
    private Bug Claim(Bug bug, JsonElement input, SqliteTransaction transaction)
    {
        var worker = Text(Property(input, "workerId"), 200, true);
        RequireWritable(bug, worker);
        bug.LockOwnerId = worker; bug.LockOwnerName = UserId; bug.LockOwnerUserId = UserId;
        bug.LockTime = DateTimeOffset.UtcNow; bug.LockExpireTime = bug.LockTime.Value.AddHours(1);
        Save("bug", bug.Id, bug, transaction); return bug;
    }

    /// <summary>活跃锁只能由原领取设备释放，过期锁可以直接清除。</summary>
    private Bug Release(Bug bug, JsonElement input, SqliteTransaction transaction)
    {
        RequireWritable(bug, Property(input, "workerId"));
        bug.LockOwnerId = null; bug.LockOwnerName = null; bug.LockOwnerUserId = null; bug.LockTime = null; bug.LockExpireTime = null;
        Save("bug", bug.Id, bug, transaction); return bug;
    }

    /// <summary>两次确认后的物理删除同时移除子记录和附件正文。</summary>
    private object DeleteBug(string id, string query, SqliteTransaction transaction)
    {
        var bug = RequireBug(id);
        if (!string.Equals(Query(query, "hardDelete"), "true", StringComparison.OrdinalIgnoreCase) || Query(query, "confirmBugId") != id)
            throw new ArgumentException(ClientText.T("删除 BUG 需要明确确认。"));
        if (BugDisplay.HasLock(bug)) throw new InvalidOperationException(ClientText.T("有处理锁时请先释放锁再删除。"));
        foreach (var item in Read<BugAttachment>("attachment").Where(x => x.BugId == id)) DeleteAttachment(item.Id, transaction);
        foreach (var item in Read<BugComment>("comment").Where(x => x.BugId == id)) Delete("comment", item.Id, transaction);
        foreach (var item in Read<BugHistory>("history").Where(x => x.BugId == id)) Delete("history", item.Id, transaction);
        Delete("bug", id, transaction); return new { };
    }

    /// <summary>每次写入读取当前记录，关闭窗口中的旧副本不能绕过锁检查。</summary>
    private Bug RequireBug(string id) => Read<Bug>("bug").SingleOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException(ClientText.T("此 BUG 已删除或已无访问权限。"));
    /// <summary>锁的最终检查由存储事务执行，而不是依赖界面禁用状态。</summary>
    private static void RequireWritable(Bug bug, string? worker)
    {
        if (BugDisplay.HasLock(bug) && bug.LockOwnerId != worker) throw new InvalidOperationException(ClientText.T("其他 Worker 持锁期间无法修改。"));
    }
    /// <summary>只读取命令契约中的字符串字段。</summary>
    private static string? Property(JsonElement input, string name) => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    /// <summary>查询字段先精确匹配名称再解码，不接受部分名称或重复参数。</summary>
    private static string? Query(string query, string name) => query.Split('&').Select(x => x.Split('=', 2))
        .Where(x => x.Length == 2 && x[0] == name).Select(x => Uri.UnescapeDataString(x[1])).SingleOrDefault();
}
