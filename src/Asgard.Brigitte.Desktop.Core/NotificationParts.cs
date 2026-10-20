namespace Asgard.Brigitte.Desktop.Core;

/// <summary>通知负载有大小上限；长正文分条发送而不是截断，保留 Unicode 字符。</summary>
public static class NotificationParts
{
    public static IReadOnlyList<string> Split(string message)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var rune in message.EnumerateRunes())
        {
            if (current.Length + rune.Utf16SequenceLength > 500) { parts.Add(current.ToString()); current.Clear(); }
            current.Append(rune.ToString());
        }
        if (current.Length > 0) parts.Add(current.ToString());
        return parts;
    }
}
