using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>旧偏好只迁移一次，SQLite 重启后仍保持账号隔离及原始文件。</summary>
public sealed class SqlitePreferenceMigrationTests
{
    [Fact]
    public void Existing_preferences_migrate_once_and_remain_separate_by_account_and_project()
    {
        var folder = Directory.CreateTempSubdirectory("brigitte-preferences-sqlite-");
        try
        {
            string PathFor(string name) => Path.Combine(folder.FullName, name);
            var boardPath = PathFor("board.json");
            var notificationPath = PathFor("notifications.json");
            var themePath = PathFor("theme.json");
            var languagePath = PathFor("language.json");
            var closePath = PathFor("close.json");
            new BoardPreferenceStore(boardPath).Set("account-a", "project-a", ["closed"]);
            new NotificationPreferenceStore(notificationPath).Set("account-a", "project-a", new NotificationRule { Enabled = false });
            new ThemePreferenceStore(themePath).Set("dark");
            new LanguagePreferenceStore(languagePath).Set("en");
            new ClosePreferenceStore(closePath).Set("tray");
            var originalBoard = File.ReadAllText(boardPath);
            var database = new LocalPreferenceDatabase(PathFor("preferences.sqlite"));
            var board = new BoardPreferenceStore(database, boardPath);
            var notices = new NotificationPreferenceStore(database, notificationPath);
            Assert.Equal("closed", board.Get("account-a", "project-a")[0]);
            Assert.Equal(BugDisplay.BoardStatuses, board.Get("account-a", "project-b"));
            Assert.Equal(BugDisplay.BoardStatuses, board.Get("account-b", "project-a"));
            Assert.False(notices.Get("account-a", "project-a").Enabled);
            Assert.True(notices.Get("account-b", "project-a").Enabled);
            Assert.Equal("dark", new ThemePreferenceStore(database, themePath).Mode);
            Assert.Equal("en", new LanguagePreferenceStore(database, languagePath).Mode);
            Assert.Equal("tray", new ClosePreferenceStore(database, closePath).Behavior);
            board.Set("account-a", "project-a", ["processing"]);
            notices.Set("account-a", "project-a", null);
            Assert.Equal("processing", new BoardPreferenceStore(database, boardPath).Get("account-a", "project-a")[0]);
            Assert.True(new NotificationPreferenceStore(database, notificationPath).Get("account-a", "project-a").Enabled);
            Assert.Equal(originalBoard, File.ReadAllText(boardPath));
            Assert.True(File.Exists(notificationPath));
        }
        finally { folder.Delete(true); }
    }

    [Fact]
    public void Corrupt_legacy_file_does_not_override_existing_sqlite_data()
    {
        var folder = Directory.CreateTempSubdirectory("brigitte-preferences-corrupt-");
        try
        {
            var path = Path.Combine(folder.FullName, "board.json");
            File.WriteAllText(path, "{broken");
            var database = new LocalPreferenceDatabase(Path.Combine(folder.FullName, "preferences.sqlite"));
            var store = new BoardPreferenceStore(database, path);
            store.Set("a", "p", ["closed"]);
            Assert.Equal("closed", new BoardPreferenceStore(database, path).Get("a", "p")[0]);
            Assert.Equal("{broken", File.ReadAllText(path));
        }
        finally { folder.Delete(true); }
    }

    [Fact]
    public void Failed_sqlite_write_keeps_the_last_in_memory_value()
    {
        var folder = Directory.CreateTempSubdirectory("brigitte-preferences-error-");
        try
        {
            var path = Path.Combine(folder.FullName, "preferences.sqlite");
            var store = new BoardPreferenceStore(new LocalPreferenceDatabase(path), Path.Combine(folder.FullName, "board.json"));
            store.Set("a", "p", ["closed"]);
            File.Move(path, path + ".backup");
            Directory.CreateDirectory(path);
            Assert.NotNull(Record.Exception(() => store.Set("a", "p", ["processing"])));
            Assert.Equal("closed", store.Get("a", "p")[0]);
        }
        finally { folder.Delete(true); }
    }
}
