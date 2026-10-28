namespace Asgard.Brigitte.Desktop.Core.Models;

/// <summary>备份文件和仅供当前删除流程使用的确认凭据。</summary>
public sealed record BugArchive(string FileName, string Base64Content, string ConfirmationToken);
