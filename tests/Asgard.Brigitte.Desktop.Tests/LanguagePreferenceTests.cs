using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证首次启动、系统语言变化、显式选择和损坏偏好的恢复。</summary>
public sealed class LanguagePreferenceTests
{
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("en-GB", "en")]
    [InlineData("ja-JP", "en")]
    [InlineData("", "en")]
    public void New_preference_follows_system_and_unsupported_languages_use_English(string system, string expected)
    {
        WithPath(path =>
        {
            var preference = new LanguagePreferenceStore(path, () => system);
            Assert.Equal("system", preference.Mode);
            Assert.Equal(expected, preference.Language);
            Assert.False(File.Exists(path));
        });
    }
    [Theory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    [InlineData("system")]
    public void Selection_survives_restart_and_system_choice_remains_dynamic(string selection)
    {
        WithPath(path =>
        {
            var system = "zh-CN";
            var preference = new LanguagePreferenceStore(path, () => system);
            preference.Set(selection);
            var restarted = new LanguagePreferenceStore(path, () => system);
            Assert.Equal(selection, restarted.Mode);
            system = "fr-FR";
            Assert.Equal(selection == "system" ? "en" : selection, restarted.Language);
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Throws<ArgumentException>(() => restarted.Set("invalid"));
            Assert.Equal(selection, restarted.Mode);
        });
    }
    [Theory]
    [InlineData("broken-json")]
    [InlineData("\"unknown\"")]
    public void Invalid_saved_preference_falls_back_to_system(string contents)
    {
        WithPath(path =>
        {
            File.WriteAllText(path, contents);
            var preference = new LanguagePreferenceStore(path, () => "en-US");
            Assert.Equal("system", preference.Mode);
            Assert.Equal("en", preference.Language);
        });
    }
    [Fact]
    public void Failed_save_keeps_the_previous_selection()
    {
        WithPath(path =>
        {
            var preference = new LanguagePreferenceStore(path, () => "zh-CN");
            preference.Set("en");
            Directory.CreateDirectory(path + ".tmp");
            try
            {
                var error = Record.Exception(() => preference.Set("zh-CN"));
                Assert.True(error is IOException or UnauthorizedAccessException);
                Assert.Equal("en", preference.Mode);
                Assert.Equal("en", new LanguagePreferenceStore(path).Mode);
            }
            finally { Directory.Delete(path + ".tmp"); }
        });
    }
    [Fact]
    public async Task Language_scopes_do_not_change_user_content_or_leak_across_async_work()
    {
        var results = await Task.WhenAll(new[] { "en", "zh-CN" }.Select(async language =>
        {
            using var scope = ClientText.UseLanguage(language);
            await Task.Yield();
            return (ClientText.T("设置"), BugDisplay.Statuses["processing"], BugDisplay.Assignee("原始账号", []));
        }));
        Assert.Equal(("Settings", "In progress", "原始账号"), results[0]);
        Assert.Equal(("设置", "处理中", "原始账号"), results[1]);
        Assert.Equal("设置", ClientText.T("设置"));
    }
    private static void WithPath(Action<string> action)
    {
        var directory = Directory.CreateTempSubdirectory("brigitte-language-test-");
        var path = Path.Combine(directory.FullName, "language.json");
        try { action(path); }
        finally { File.Delete(path); File.Delete(path + ".tmp"); directory.Delete(); }
    }
}
