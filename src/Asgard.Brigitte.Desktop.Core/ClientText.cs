using System.Globalization;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>桌面内置文案使用统一资源；用户输入、服务端正文和协议字段不参与翻译。</summary>
public static class ClientText
{
    private static readonly AsyncLocal<string?> Override = new();
    private static readonly IReadOnlyDictionary<string, string> English = LoadEnglish();
    private static string _language = Resolve(CultureInfo.CurrentUICulture.Name);
    /// <summary>临时作用域只用于指定语言的渲染，不更改操作系统文化设置。</summary>
    public static string Language => Override.Value ?? Volatile.Read(ref _language);
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "zh-CN" ? "zh-CN" : "en-US");
    /// <summary>简体中文覆盖中文系统；其他系统使用英文。</summary>
    public static string Resolve(string? language) => language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true ? "zh-CN" : "en";
    public static void SetLanguage(string language) => Volatile.Write(ref _language, Resolve(language));
    /// <summary>同一摘要可分别生成两种语言，异步和并行操作互不覆盖。</summary>
    public static IDisposable UseLanguage(string language)
    {
        var previous = Override.Value;
        Override.Value = Resolve(language);
        return new LanguageScope(() => Override.Value = previous);
    }
    public static string T(string key, params object?[] arguments) => Translate(key, Language, arguments);
    /// <summary>仅查找完整内置键，绝不对子字符串或用户内容做替换。</summary>
    public static string Translate(string key, string language, params object?[] arguments)
    {
        var chinese = Resolve(language) == "zh-CN";
        var text = !chinese && English.TryGetValue(key, out var translated) ? translated : key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.GetCultureInfo(chinese ? "zh-CN" : "en-US"), text, arguments);
    }
    private static IReadOnlyDictionary<string, string> LoadEnglish()
    {
        var assembly = typeof(ClientText).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Localization.en.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!) result.Add(pair.Key, pair.Value);
        }
        return result;
    }
}
