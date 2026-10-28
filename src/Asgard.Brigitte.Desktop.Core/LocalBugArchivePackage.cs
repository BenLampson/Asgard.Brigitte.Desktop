using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Asgard.Brigitte.Desktop.Core;

/// <summary>本机模式输出完整 SQLite 快照及便于阅读的 Excel。</summary>
internal static class LocalBugArchivePackage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static byte[] Create(LocalBugArchiveSnapshot snapshot) => Create([snapshot]);

    /// <summary>多条 BUG 共用一个 SQLite 文件和 Excel 工作簿。</summary>
    public static byte[] Create(IReadOnlyList<LocalBugArchiveSnapshot> snapshots)
    {
        var directory = Path.Combine(Path.GetTempPath(), "brigitte-local-archive-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var path = Path.Combine(directory, "bugs.sqlite");
            CreateDatabase(path, snapshots);
            using var result = new MemoryStream();
            using (var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
            {
                Add(zip, "bugs.sqlite", File.ReadAllBytes(path));
                Add(zip, "bugs.xlsx", CreateSpreadsheet(snapshots));
                Add(zip, "说明.txt", Encoding.UTF8.GetBytes("SQLite 的 records 包含完整 JSON，附件正文保存在 attachment_data.content。\r\nExcel 为阅读视图，超长单元格请查看 SQLite 中的完整内容。时间使用 UTC。\r\n"));
            }
            return result.ToArray();
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CreateDatabase(string path, IReadOnlyList<LocalBugArchiveSnapshot> snapshots)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE records(kind TEXT NOT NULL,id TEXT NOT NULL,json TEXT NOT NULL,PRIMARY KEY(kind,id)); CREATE TABLE attachment_data(id TEXT PRIMARY KEY,content BLOB NOT NULL);";
            command.ExecuteNonQuery();
        }
        foreach (var snapshot in snapshots)
        {
            Save(connection, transaction, "bug", snapshot.Bug.Id, snapshot.Bug);
            foreach (var item in snapshot.Comments) Save(connection, transaction, "comment", item.Id, item);
            foreach (var item in snapshot.Histories) Save(connection, transaction, "history", item.Id, item);
            foreach (var item in snapshot.Attachments)
            {
                Save(connection, transaction, "attachment", item.Id, item);
                using var command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = "INSERT INTO attachment_data(id,content) VALUES($id,$content)";
                command.Parameters.AddWithValue("$id", item.Id);
                command.Parameters.AddWithValue("$content", Convert.FromBase64String(item.Base64Content));
                command.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    }

    private static void Save(SqliteConnection connection, SqliteTransaction transaction, string kind, string id, object value)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO records(kind,id,json) VALUES($kind,$id,$json)";
        command.Parameters.AddWithValue("$kind", kind); command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(value, value.GetType(), Json)); command.ExecuteNonQuery();
    }

    private static byte[] CreateSpreadsheet(IReadOnlyList<LocalBugArchiveSnapshot> snapshots)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook(new Sheets());
            Sheet(workbook, "BUG", new[] { new string?[] { "ID", "项目 ID", "标题", "状态", "优先级", "负责人", "描述", "详细内容", "涉及服务", "部署说明", "来源", "来源引用", "创建时间 UTC", "更新时间 UTC" } }
                .Concat(snapshots.Select(x => x.Bug).Select(bug => new string?[] { bug.Id, bug.ProjectId, bug.Title, bug.Status, bug.Priority, bug.AssigneeId, bug.Description, bug.Content, string.Join("\n", bug.AffectedServices), bug.DeploymentNotes, bug.Source, bug.SourceRef, bug.CreateTime.ToString("O"), bug.UpdateTime?.ToString("O") })));
            Sheet(workbook, "评论", new[] { new string?[] { "ID", "BUG ID", "内容", "类型", "作者类型", "创建时间 UTC" } }
                .Concat(snapshots.SelectMany(snapshot => snapshot.Comments).Select(x => new string?[] { x.Id, x.BugId, x.Content, x.CommentType, x.AuthorType, x.CreateTime.ToString("O") })));
            Sheet(workbook, "状态历史", new[] { new string?[] { "ID", "BUG ID", "原状态", "新状态", "原因", "操作者类型", "创建时间 UTC" } }
                .Concat(snapshots.SelectMany(snapshot => snapshot.Histories).Select(x => new string?[] { x.Id, x.BugId, x.FromStatus, x.ToStatus, x.Reason, x.OperatorType, x.CreateTime.ToString("O") })));
            Sheet(workbook, "附件", new[] { new string?[] { "ID", "BUG ID", "文件名", "类型", "字节数", "创建时间 UTC", "文件内容" } }
                .Concat(snapshots.SelectMany(snapshot => snapshot.Attachments).Select(x => new string?[] { x.Id, x.BugId, x.FileName, x.ContentType, x.SizeBytes.ToString(), x.CreateTime.ToString("O"), "见 SQLite attachment_data.content" })));
            workbook.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static void Sheet(WorkbookPart workbook, string name, IEnumerable<string?[]> rows)
    {
        var part = workbook.AddNewPart<WorksheetPart>(); var data = new SheetData();
        foreach (var values in rows)
        {
            var row = new Row();
            foreach (var value in values)
                row.Append(new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(Clean(value)) { Space = SpaceProcessingModeValues.Preserve }) });
            data.Append(row);
        }
        part.Worksheet = new Worksheet(data);
        var sheets = workbook.Workbook.GetFirstChild<Sheets>()!;
        sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = (uint)(sheets.ChildElements.Count + 1), Name = name });
    }

    private static string Clean(string? value)
    {
        var result = new StringBuilder();
        foreach (var rune in (value ?? "").EnumerateRunes())
        {
            if (rune.Value < 0x10000 && !XmlConvert.IsXmlChar((char)rune.Value)) continue;
            if (result.Length + rune.Utf16SequenceLength > 32767) break;
            result.Append(rune.ToString());
        }
        return result.ToString();
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(bytes);
    }
}
