using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>本地顺序升级补齐、持久化和账号隔离。</summary>
public sealed class BoardPreferenceTests
{
    [Fact]
    public void Normalization_keeps_preference_and_appends_new_lanes_once()
    {
        var order = BoardPreferenceStore.Normalize(["closed", "processing", "closed", "obsolete", null!]);
        Assert.Equal("closed", order[0]); Assert.Equal("processing", order[1]);
        Assert.Equal(BugDisplay.BoardStatuses.Length, order.Length);
        Assert.Equal(BugDisplay.BoardStatuses.Order(), order.Order());
    }
    [Fact]
    public void Order_survives_restart_and_is_scoped_to_account_and_project()
    {
        var directory = Directory.CreateTempSubdirectory("brigitte-board-test-");
        var path = Path.Combine(directory.FullName, "board.json");
        try
        {
            var store = new BoardPreferenceStore(path);
            store.Set("account-a", "project-a", ["closed", "processing"]);
            var restarted = new BoardPreferenceStore(path);
            Assert.Equal("closed", restarted.Get("account-a", "project-a")[0]);
            Assert.Equal(BugDisplay.BoardStatuses, restarted.Get("account-b", "project-a"));
            Assert.Equal(BugDisplay.BoardStatuses, restarted.Get("account-a", "project-b"));
            restarted.Set("account-a", "project-a", BugDisplay.BoardStatuses);
            Assert.Equal(BugDisplay.BoardStatuses, new BoardPreferenceStore(path).Get("account-a", "project-a"));
        }
        finally { File.Delete(path); File.Delete(path + ".tmp"); directory.Delete(); }
    }
    [Fact]
    public void Failed_save_preserves_last_order()
    {
        var directory = Directory.CreateTempSubdirectory("brigitte-board-failure-");
        var path = Path.Combine(directory.FullName, "board.json");
        try
        {
            var store = new BoardPreferenceStore(path);
            store.Set("a", "p", ["closed"]);
            Directory.CreateDirectory(path + ".tmp");
            var error = Record.Exception(() => store.Set("a", "p", ["processing"]));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.Equal("closed", store.Get("a", "p")[0]);
        }
        finally { Directory.Delete(path + ".tmp"); File.Delete(path); directory.Delete(); }
    }
}
