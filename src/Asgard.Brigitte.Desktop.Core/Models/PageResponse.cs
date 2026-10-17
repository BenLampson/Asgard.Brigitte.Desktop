namespace Asgard.Brigitte.Desktop.Core.Models;
/// <summary>保留服务端分页契约。</summary>
public sealed class PageResponse<T> : ApiResponse<List<T>>
{
    /// <summary>总记录数。</summary>
    public long TotalCount { get; set; }
    /// <summary>当前页。</summary>
    public int Page { get; set; }
    /// <summary>每页条数。</summary>
    public int Size { get; set; }
}
