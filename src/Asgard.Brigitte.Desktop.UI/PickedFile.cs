namespace Asgard.Brigitte.Desktop.UI;
/// <summary>用户明确选择的待上传附件。</summary>
public sealed record PickedFile(string FileName, string ContentType, byte[] Bytes);
