using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证系统预览副本的内容、类型、生命周期和失败行为。</summary>
public sealed class AttachmentPreviewTests
{
    /// <summary>同名附件各自创建副本，伪造的可执行扩展名不能决定系统关联。</summary>
    [Fact]
    public async Task Open_preserves_bytes_and_uses_a_safe_image_extension_without_overwriting_earlier_previews()
    {
        using var local = new StandaloneTestWorkspace();
        var paths = new List<string>();
        var first = new byte[] { 0, 1, 2, 255 };
        var second = new byte[] { 3, 4, 5 };
        async Task<bool> Open(string path)
        {
            Assert.True(File.Exists(path));
            paths.Add(path);
            Assert.Equal(paths.Count == 1 ? first : second, await File.ReadAllBytesAsync(path));
            return true;
        }

        await AttachmentPreview.OpenAsync(local.DirectoryPath, "../截图.exe", "image/png", first, Open);
        await AttachmentPreview.OpenAsync(local.DirectoryPath, "../截图.exe", "image/png", second, Open);

        Assert.Equal(2, paths.Count);
        Assert.NotEqual(paths[0], paths[1]);
        Assert.All(paths, path => Assert.Equal("截图.png", Path.GetFileName(path)));
        Assert.Equal(first, await File.ReadAllBytesAsync(paths[0], TestContext.Current.CancellationToken));
        Assert.Equal(second, await File.ReadAllBytesAsync(paths[1], TestContext.Current.CancellationToken));
    }

    /// <summary>文件名只保留当前图片类型的扩展名，并兼容路径、缺少扩展名和 Windows 设备名。</summary>
    [Theory]
    [InlineData("C:\\temp\\photo.jpeg", "image/jpeg", "photo.jpeg")]
    [InlineData("CON.jpg", "image/jpeg", "_CON.jpg")]
    [InlineData("picture.png", "image/webp", "picture.webp")]
    [InlineData("photo", "IMAGE/PNG", "photo.png")]
    [InlineData("../", "image/gif", "attachment.gif")]
    public async Task Preview_file_name_matches_the_image_type(string original, string contentType, string expected)
    {
        using var local = new StandaloneTestWorkspace();
        string? staged = null;
        await AttachmentPreview.OpenAsync(local.DirectoryPath, original, contentType, [1], path => { staged = path; return Task.FromResult(true); });
        Assert.Equal(expected, Path.GetFileName(staged));
    }

    /// <summary>系统无法打开时向用户报告失败，不保留无用的附件副本。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Open_failure_removes_the_copy_and_reports_an_actionable_error(bool throwError)
    {
        using var language = ClientText.UseLanguage("zh-CN");
        using var local = new StandaloneTestWorkspace();
        string? staged = null;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AttachmentPreview.OpenAsync(local.DirectoryPath, "screen.jpg", "image/jpeg", [1, 2], path =>
        {
            staged = path;
            if (throwError) throw new IOException("internal launcher error");
            return Task.FromResult(false);
        }));

        Assert.Contains("系统默认程序", error.Message);
        Assert.NotNull(staged);
        Assert.False(File.Exists(staged));
        Assert.False(Directory.Exists(Path.GetDirectoryName(staged)));
    }

    /// <summary>仅图片类型可交给系统打开，不能通过文件名绕过类型限制。</summary>
    [Fact]
    public async Task Unsupported_type_is_rejected_before_writing_or_launching()
    {
        using var local = new StandaloneTestWorkspace();
        var launched = false;
        Assert.False(AttachmentPreview.CanPreview("application/octet-stream"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AttachmentPreview.OpenAsync(local.DirectoryPath, "image.png", "application/octet-stream", [1], _ => { launched = true; return Task.FromResult(true); }));
        Assert.False(launched);
        Assert.False(Directory.Exists(Path.Combine(local.DirectoryPath, "attachment-previews")));
    }

    /// <summary>只尝试清理超过七天的应用自有副本，保留近期及其他目录。</summary>
    [Fact]
    public async Task Opening_an_image_removes_old_preview_copies_only()
    {
        using var local = new StandaloneTestWorkspace();
        var root = Path.Combine(local.DirectoryPath, "attachment-previews");
        var old = Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("N"))).FullName;
        var recent = Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("N"))).FullName;
        var unrelated = Directory.CreateDirectory(Path.Combine(root, "unrelated")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(old, "old.png"), [1], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(recent, "recent.png"), [2], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(unrelated, "keep.png"), [3], TestContext.Current.CancellationToken);
        Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-8));
        Directory.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddDays(-8));

        await AttachmentPreview.OpenAsync(local.DirectoryPath, "new.png", "image/png", [4], _ => Task.FromResult(true));

        Assert.False(Directory.Exists(old));
        Assert.True(File.Exists(Path.Combine(recent, "recent.png")));
        Assert.True(File.Exists(Path.Combine(unrelated, "keep.png")));
    }
}
