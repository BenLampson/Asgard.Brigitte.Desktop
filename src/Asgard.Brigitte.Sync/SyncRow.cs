namespace Asgard.Brigitte.Sync;

/// <summary>服务端授权后的记录投影；空内容代表删除，不包含附件正文或凭据。</summary>
public sealed record SyncRow(string Key, string Hash, string? Json);
