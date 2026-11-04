namespace Asgard.Brigitte.Desktop.Core;

/// <summary>为系统默认图片程序准备临时文件，不在客户端内渲染附件。</summary>
public static class AttachmentPreview
{
    private const string CacheFolder = "attachment-previews";
    private const string OpenError = "无法使用系统默认程序打开图片。请检查此图片类型的默认应用后重试。";

    /// <summary>沿用图片预览范围，避免将其他类型的附件当作可执行文件打开。</summary>
    public static bool CanPreview(string? contentType) => Extension(contentType) is not null;

    /// <summary>为每次打开创建独立副本，打开失败时删除副本，成功时留给外部程序读取。</summary>
    public static async Task OpenAsync(string cacheDirectory, string fileName, string contentType, byte[] content, Func<string, Task<bool>> openFile)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(openFile);
        var preferredExtension = Extension(contentType) ?? throw new InvalidOperationException(ClientText.T("不支持预览此附件。"));
        var root = Path.Combine(cacheDirectory, CacheFolder);
        Directory.CreateDirectory(root);
        RemoveExpired(root);
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, SafeFileName(fileName, contentType, preferredExtension));
            await File.WriteAllBytesAsync(path, content);
            bool opened;
            try { opened = await openFile(path); }
            catch (Exception ex) when (ex is not OperationCanceledException) { throw new InvalidOperationException(ClientText.T(OpenError), ex); }
            if (!opened) throw new InvalidOperationException(ClientText.T(OpenError));
        }
        catch
        {
            TryDelete(directory);
            throw;
        }
    }

    /// <summary>只信任支持的图片类型决定扩展名，禁止路径和伪造扩展名影响系统关联。</summary>
    private static string SafeFileName(string? fileName, string contentType, string preferredExtension)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        var originalExtension = Path.GetExtension(name);
        var extension = contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) && originalExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? ".jpeg" : preferredExtension;
        var stem = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(originalExtension)) stem = name;
        var invalid = Path.GetInvalidFileNameChars();
        stem = new string(stem.Select(c => char.IsControl(c) || invalid.Contains(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        if (stem.Length > 120) stem = stem[..120].TrimEnd(' ', '.');
        if (stem.Length == 0) stem = "attachment";
        var deviceName = stem.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(deviceName, StringComparer.OrdinalIgnoreCase)) stem = "_" + stem;
        return stem + extension;
    }

    private static string? Extension(string? contentType) => contentType?.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => null
    };

    /// <summary>下次预览时尽力清理七天以前的独立副本，不递归删除其他内容。</summary>
    private static void RemoveExpired(string root)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                if (Guid.TryParseExact(Path.GetFileName(directory), "N", out _) && Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddDays(-7)) TryDelete(directory);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>跳过链接及正在使用的文件；清理失败不会中断后续预览。</summary>
    private static void TryDelete(string directory)
    {
        try
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
