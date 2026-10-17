namespace Asgard.Brigitte.Sync;

/// <summary>客户端只上传内容指纹用于对账，不能上传业务数据。</summary>
public sealed record SyncStamp(string Key, string Hash);
