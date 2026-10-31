using System.Text.Json.Nodes;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>从本次提交前后的投影生成动态；正文仅用于比较，绝不复制进通知。</summary>
public sealed partial class LocalWorkspaceDatabase
{
    /// <summary>同批次按 BUG 合并，保留具体状态、领取者和回复类型；纯续锁与审计字段补齐静默。</summary>
    private SyncNotice[] BuildNotices(string batchId)
    {
        SyncNotice[] chinese, english;
        using (ClientText.UseLanguage("zh-CN")) chinese = BuildNoticesCore(batchId);
        using (ClientText.UseLanguage("en")) english = BuildNoticesCore(batchId);
        var translated = english.ToDictionary(x => x.Id);
        return chinese.Select(x =>
        {
            if (!translated.TryGetValue(x.Id, out var other)) return x;
            // 只有逐项类型一致才合并翻译，避免不同语言的去重结果错配。
            var matching = x.Parts.Length == other.Parts.Length && x.Parts.Zip(other.Parts)
                .All(pair => pair.First.Change == pair.Second.Change && pair.First.TargetStatus == pair.Second.TargetStatus);
            return x with
            {
                EnglishMessage = other.Message,
                EnglishSubject = other.Subject,
                Parts = matching ? x.Parts.Zip(other.Parts, (a, b) => a with { EnglishMessage = b.Message }).ToArray() : []
            };
        }).ToArray();
    }
    private SyncNotice[] BuildNoticesCore(string batchId)
    {
        var changes = new Dictionary<string, (JsonObject? Before, JsonObject? After)>();
        using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT s.key,r.json,s.json FROM staging s LEFT JOIN records r ON r.key=s.key ORDER BY s.key";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) changes.Add(reader.GetString(0), (
                reader.IsDBNull(1) ? null : JsonNode.Parse(reader.GetString(1))!.AsObject(),
                reader.IsDBNull(2) ? null : JsonNode.Parse(reader.GetString(2))!.AsObject()));
        }
        var records = new Dictionary<string, JsonObject?>();
        JsonObject? Record(string key)
        {
            if (changes.TryGetValue(key, out var change)) return change.After ?? change.Before;
            if (!records.TryGetValue(key, out var record)) records[key] = record = ReadNoticeRecord(key);
            return record;
        }
        string Person(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            // 仅用目录中的明确映射；旧的 MCP 审计前缀不在系统通知中展示。
            if (id.StartsWith("mcp:", StringComparison.Ordinal)) return ClientText.T("MCP 操作者");
            var user = Record("user/" + id);
            return NoticeLabel(Text(user, "displayName") is { Length: > 0 } name ? name :
                Text(user, "username") is { Length: > 0 } username ? username : id);
        }
        string Actor(string id) => Person(id) is { Length: > 0 } name ? name + " " : "";
        // 领取摘要只使用服务端绑定的用户，旧 Worker 自报名称不能冒充真人。
        string Claimant(JsonObject? bug) => Person(Text(bug, "lockOwnerUserId")) is { Length: > 0 } name
            ? name : ClientText.T("领取用户未知");
        string ProjectLabel(JsonObject? bug)
        {
            var id = Text(bug, "projectId");
            if (id.Length == 0) return ClientText.T("项目未提供");
            // 优先同批次的项目资料；删除 BUG 或项目时仍可使用提交前的归属。
            var project = Record("project/" + id);
            return NoticeLabel(Text(project, "name") is { Length: > 0 } name ? name :
                Text(project, "code") is { Length: > 0 } code ? code : ClientText.T("项目 #") + BugDisplay.ShortId(id));
        }
        var summaries = new Dictionary<string, List<SyncNoticePart>>();
        var types = new Dictionary<string, NotificationChange>();
        var targetStatuses = new Dictionary<string, HashSet<string>>();
        // 类型与目标状态跟随摘要生成；不能解析翻译后的自然语言来判断提醒。
        void Add(string key, string message, NotificationChange type, string? targetStatus = null)
        {
            if (noticePreferences is not null && _account is not null)
            {
                var projectId = key.StartsWith("bug/", StringComparison.Ordinal) ? Text(Record(key), "projectId") : key.StartsWith("project/", StringComparison.Ordinal) ? key[8..] : "";
                var candidate = new SyncNotice("", "", DateTimeOffset.MinValue, false)
                {
                    ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId,
                    Changes = type,
                    TargetStatuses = targetStatus is null ? [] : [targetStatus]
                };
                if (!noticePreferences.Allows(_account, candidate)) return;
            }
            if (!summaries.TryGetValue(key, out var messages)) summaries[key] = messages = [];
            if (!messages.Any(item => item.Message == message && item.Change == type && item.TargetStatus == targetStatus))
                messages.Add(new(message, type, targetStatus));
            types[key] = types.GetValueOrDefault(key) | type;
            if (targetStatus is not null)
            {
                if (!targetStatuses.TryGetValue(key, out var statuses)) targetStatuses[key] = statuses = [];
                statuses.Add(targetStatus);
            }
        }
        var histories = changes.Where(x => x.Key.StartsWith("history/") && x.Value.Before is null && x.Value.After is not null)
            .Select(x => x.Value.After!).OrderBy(x => Text(x, "createTime")).ToArray();
        foreach (var (key, (before, after)) in changes)
        {
            if (key.StartsWith("bug/"))
            {
                if (after is null) { Add(key, ClientText.T("已移出当前工作区"), NotificationChange.Removed); continue; }
                if (before is null)
                {
                    Add(key, Actor(Text(after, "createdBy")) + ClientText.T("提交了 BUG"), NotificationChange.Created);
                    if (Text(after, "status") is { Length: > 0 } status && status != "waiting")
                        Add(key, ClientText.T("当前状态：") + BugDisplay.Statuses.GetValueOrDefault(status, status), NotificationChange.Created);
                    if (Text(after, "lockOwnerId").Length > 0)
                        Add(key, ClientText.T("由 {0} 领取", Claimant(after)), NotificationChange.Created);
                    continue;
                }
                var actor = Actor(Text(after, "updatedBy"));
                if (Changed(before, after, "status") && !histories.Any(x => Text(x, "bugId") == key[4..]))
                    Add(key, StatusNotice(actor, Text(before, "status"), Text(after, "status")), NotificationChange.Status, Text(after, "status"));
                if (Changed(before, after, "lockOwnerId", "lockOwnerUserId"))
                {
                    var owner = Text(after, "lockOwnerId");
                    Add(key, owner.Length > 0 ? ClientText.T("由 {0} 领取", Claimant(after)) :
                        ClientText.T("{0} 的领取已释放", Claimant(before)), NotificationChange.Assignment);
                }
                if (Changed(before, after, "title")) Add(key, actor + ClientText.T("修改了标题"), NotificationChange.Details);
                if (Changed(before, after, "description", "content")) Add(key, actor + ClientText.T("修改了内容"), NotificationChange.Details);
                if (Changed(before, after, "priority")) Add(key, actor + ClientText.T("将优先级改为") + BugDisplay.Priorities.GetValueOrDefault(Text(after, "priority"), Text(after, "priority")), NotificationChange.Details);
                if (Changed(before, after, "assigneeId")) Add(key, actor + ClientText.T("将负责人改为") + (Person(Text(after, "assigneeId")) is { Length: > 0 } assignee ? assignee : ClientText.T("未指派")), NotificationChange.Assignment);
                if (Changed(before, after, "affectedServices")) Add(key, actor + ClientText.T("修改了涉及服务"), NotificationChange.Details);
                if (Changed(before, after, "deploymentNotes")) Add(key, actor + ClientText.T("修改了部署说明"), NotificationChange.Details);
                if (Changed(before, after, "source", "sourceRef")) Add(key, actor + ClientText.T("修改了来源信息"), NotificationChange.Details);
            }
            else if (key.StartsWith("project/"))
            {
                if (after is null) Add(key, ClientText.T("已移出当前工作区"), NotificationChange.Project);
                else if (before is null) Add(key, ClientText.T("已加入当前工作区"), NotificationChange.Project);
                else if (Changed(before, after, "name", "code", "description", "status", "ownerId", "memberUserIds")) Add(key, ClientText.T("项目资料已更新"), NotificationChange.Project);
            }
            else if (key.StartsWith("comment/") || key.StartsWith("attachment/"))
            {
                var bugKey = "bug/" + Text(after ?? before, "bugId");
                // 删除 BUG 时不再重复列出随它移除的回复和附件。
                if (changes.TryGetValue(bugKey, out var bug) && bug.After is null) continue;
                if (key.StartsWith("attachment/"))
                {
                    if (before is null || after is null || Changed(before, after, "fileName", "sizeBytes", "contentType"))
                        Add(bugKey, after is null ? ClientText.T("删除了附件") : before is null ? ClientText.T("添加了附件") : ClientText.T("更新了附件"), NotificationChange.Attachments);
                }
                else if (after is null) Add(bugKey, ClientText.T("删除了回复"), NotificationChange.Comments);
                else if (before is null) Add(bugKey, Actor(Text(after, "authorId")) + CommentNotice(Text(after, "commentType")), NotificationChange.Comments);
                else if (Changed(before, after, "content", "commentType")) Add(bugKey, ClientText.T("修改了回复"), NotificationChange.Comments);
            }
        }
        foreach (var history in histories)
        {
            var key = "bug/" + Text(history, "bugId");
            if (changes.TryGetValue(key, out var bug) && (bug.Before is null || bug.After is null)) continue;
            Add(key, StatusNotice(Actor(Text(history, "operatorId")), Text(history, "fromStatus"), Text(history, "toStatus")), NotificationChange.Status, Text(history, "toStatus"));
        }
        var now = DateTimeOffset.UtcNow;
        return summaries.Select(x =>
        {
            var isBug = x.Key.StartsWith("bug/");
            var record = Record(x.Key);
            var label = NoticeLabel(Text(record, isBug ? "title" : "name"));
            var subject = isBug ? $"【{ProjectLabel(record)}】BUG #{BugDisplay.ShortId(x.Key[4..])}" + (label.Length > 0 ? " · " + label : "") : ClientText.T("项目 {0}", label);
            return new SyncNotice(batchId + ":" + x.Key, subject.TrimEnd() + "\n" + string.Join("；", x.Value.Select(item => item.Message)), now, false)
            {
                ProjectId = isBug ? Text(record, "projectId") : x.Key[8..],
                Changes = types[x.Key],
                TargetStatuses = targetStatuses.TryGetValue(x.Key, out var statuses) ? statuses.ToArray() : [],
                Subject = subject.TrimEnd(),
                Parts = x.Value.ToArray()
            };
        }).ToArray();
    }
    /// <summary>补充关联项目、BUG 和用户目录，读取的仍然是本次提交所依据的本地快照。</summary>
    private JsonObject? ReadNoticeRecord(string key)
    {
        using var cmd = Connection.CreateCommand(); cmd.CommandText = "SELECT json FROM records WHERE key=$key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() is string json ? JsonNode.Parse(json)!.AsObject() : null;
    }
    /// <summary>只比较业务字段，排除心跳时间、续锁时间以及新增展示字段。</summary>
    private static bool Changed(JsonObject before, JsonObject after, params string[] fields) =>
        fields.Any(field => !JsonNode.DeepEquals(before[field], after[field]));
    /// <summary>同步字段缺失时使用空值，兼容尚未升级的服务端。</summary>
    private static string Text(JsonObject? record, string field) => record?[field]?.ToString() ?? "";
    /// <summary>通知中的标题与人名压成单行，保留完整内容。</summary>
    private static string NoticeLabel(string value)
    {
        var text = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text;
    }
    /// <summary>状态显示中文名称，操作人缺失时不猜测当前领取者就是操作者。</summary>
    private static string StatusNotice(string actor, string from, string to) => actor +
        (from.Length == 0 ? ClientText.T("将状态改为") : ClientText.T("将状态从") + BugDisplay.Statuses.GetValueOrDefault(from, from) + ClientText.T("改为")) + BugDisplay.Statuses.GetValueOrDefault(to, to);
    /// <summary>提示回复或记录类别，不含具体回复正文。</summary>
    private static string CommentNotice(string type) => type switch
    {
        "analysis" => ClientText.T("添加了分析记录"), "fix" => ClientText.T("添加了修复记录"), "regression" => ClientText.T("添加了回归记录"),
        "question" => ClientText.T("添加了补充问题"), _ => ClientText.T("添加了回复")
    };
}
