using System.Text.RegularExpressions;
using Asgard.Brigitte.Desktop.Core.Models;

namespace Asgard.Brigitte.Desktop.UI;

/// <summary>仅把正文中与当前项目真实反馈一致的完整 ID 解析为标题。</summary>
public static class BugReferenceResolver
{
    private static readonly Regex BugId = new(@"(?<![0-9a-z-])[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?![0-9a-z-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>按正文出现顺序去重；未知 ID、当前反馈和非 BUG 标识保持原文。</summary>
    public static IReadOnlyList<Bug> Resolve(string? content, IReadOnlyList<Bug> candidates, string? currentBugId)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];
        var byId = candidates.Where(b => !string.IsNullOrWhiteSpace(b.Id))
            .GroupBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Bug>();
        foreach (Match match in BugId.Matches(content))
        {
            if (string.Equals(match.Value, currentBugId, StringComparison.OrdinalIgnoreCase) || !seen.Add(match.Value)) continue;
            if (byId.TryGetValue(match.Value, out var bug)) result.Add(bug);
        }
        return result;
    }
}
