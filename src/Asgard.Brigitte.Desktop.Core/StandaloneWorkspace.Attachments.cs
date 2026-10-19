using Microsoft.Data.Sqlite;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>附件正文保存到本机 SQLite，预览、下载和删除全部不依赖远程服务。</summary>
public sealed partial class StandaloneWorkspace
{
    /// <summary>每个附件限制 50 MB，解码前先限制编码长度，元数据不保存正文。</summary>
    private BugAttachment AddAttachment(Bug bug, JsonElement input, SqliteTransaction transaction)
    {
        const int limit = 50 * 1024 * 1024;
        var encoded = Property(input, "base64Content") ?? "";
        if (encoded.Length > ((limit + 2) / 3) * 4) throw new ArgumentException(ClientText.T("单附件不能超过 50 MB。"));
        var bytes = Convert.FromBase64String(encoded);
        if (bytes.Length > limit) throw new ArgumentException(ClientText.T("单附件不能超过 50 MB。"));
        var attachment = new BugAttachment { Id = NewId(), BugId = bug.Id,
            FileName = Text(Path.GetFileName(Property(input, "fileName")), 255, true),
            ContentType = Text(Property(input, "contentType"), 200, true), SizeBytes = bytes.Length, CreateTime = DateTimeOffset.UtcNow };
        Save("attachment", attachment.Id, attachment, transaction);
        using var command = Connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO attachment_data(id,content) VALUES($id,$content)";
        command.Parameters.AddWithValue("$id", attachment.Id); command.Parameters.AddWithValue("$content", bytes); command.ExecuteNonQuery();
        return attachment;
    }

    /// <summary>正文和元数据同时删除，避免清理失败留下无效列表项。</summary>
    private void DeleteAttachment(string id, SqliteTransaction transaction)
    {
        using var command = Connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "DELETE FROM attachment_data WHERE id=$id";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
        Delete("attachment", id, transaction);
    }
}
