namespace Asgard.Brigitte.Desktop.Core;

/// <summary>所有业务查询只读 SQLite，附件正文由独立下载方法取得。</summary>
public sealed class LocalWorkspaceReader(LocalWorkspaceDatabase database)
{
    public List<Project> Projects() => database.Read<Project>("project").OrderBy(x => x.Name).ToList();
    public List<Bug> Bugs(string? project = null) => database.Read<Bug>("bug").Where(x => project is null || x.ProjectId == project).OrderByDescending(x => x.CreateTime).ThenByDescending(x => x.Id).ToList();
    public BugDetail Detail(string id) => new()
    {
        Bug = Bugs().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException(ClientText.T("此 BUG 已删除或已无访问权限。")),
        Comments = database.Read<BugComment>("comment").Where(x => x.BugId == id).OrderBy(x => x.CreateTime).ToArray(),
        Histories = database.Read<BugHistory>("history").Where(x => x.BugId == id).OrderBy(x => x.CreateTime).ToArray(),
        Attachments = database.Read<BugAttachment>("attachment").Where(x => x.BugId == id).OrderBy(x => x.CreateTime).ToArray()
    };
    public List<ProjectStatistics> Statistics()
    {
        var bugs = Bugs();
        return Projects().Select(p => new ProjectStatistics { ProjectId = p.Id, Total = bugs.LongCount(x => x.ProjectId == p.Id),
            HighPriority = bugs.LongCount(x => x.ProjectId == p.Id && x.Priority is "critical" or "high"),
            Unresolved = bugs.LongCount(x => x.ProjectId == p.Id && BugDisplay.IsUnresolved(x.Status)),
            UnresolvedHighPriority = bugs.LongCount(x => x.ProjectId == p.Id && BugDisplay.IsUnresolved(x.Status) && x.Priority is "critical" or "high"),
            StatusCounts = bugs.Where(x => x.ProjectId == p.Id).GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.LongCount()) }).ToList();
    }
    public List<PromptAction> Actions(string status) => database.Read<LocalPromptTemplate>("prompt")
        .Where(x => x.Enabled && x.Category == "bug" && x.ApplicableStatuses.Contains(status, StringComparer.OrdinalIgnoreCase)).OrderBy(x => x.SortOrder)
        .Select(x => new PromptAction { Key = x.Key, Name = x.Name, Action = x.Action, ActionLabel = string.IsNullOrWhiteSpace(x.ActionLabel) ? x.Name : x.ActionLabel,
            IsPrimary = x.PrimaryStatuses.Contains(status, StringComparer.OrdinalIgnoreCase), SortOrder = x.SortOrder }).ToList();
    public string Prompt(string key, Dictionary<string, string?> variables)
    {
        var content = database.Read<LocalPromptTemplate>("prompt").FirstOrDefault(x => x.Key == key)?.Content ?? throw new InvalidOperationException(ClientText.T("提示词尚未同步。"));
        foreach (var (name, value) in variables) content = content.Replace("{{" + name + "}}", value ?? "", StringComparison.Ordinal);
        return content;
    }
    public T Get<T>(string path)
    {
        object result = path switch
        {
            "bugs/statistics" => Statistics(), "identity/access" => database.Read<CurrentAccess>("access").Single(),
            "mcp-api-keys" => database.Read<McpKey>("key"),
            _ when path.StartsWith("projects/") => Projects().FirstOrDefault(x => x.Id == Uri.UnescapeDataString(path[9..])) ?? throw new InvalidOperationException(ClientText.T("项目已删除或已无访问权限。")),
            _ => throw new InvalidOperationException(ClientText.T("不支持的本地查询。"))
        };
        return (T)result;
    }
    public List<T> All<T>(string path) => path switch
    {
        "projects" => (List<T>)(object)Projects(), "identity/users" => database.Read<T>("user"),
        _ when path.StartsWith("bugs?projectId=") => (List<T>)(object)Bugs(Uri.UnescapeDataString(path[15..])),
        _ => throw new InvalidOperationException(ClientText.T("不支持的本地集合查询。"))
    };
}
