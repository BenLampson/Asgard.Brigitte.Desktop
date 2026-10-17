namespace Asgard.Brigitte.Desktop.Core.Models;
/// <summary>现有 API 统一响应。</summary>
public class ApiResponse<T>
{
    /// <summary>业务响应码。</summary>
    public int Code { get; set; }
    /// <summary>安全错误说明。</summary>
    public string Message { get; set; } = "";
    /// <summary>响应数据。</summary>
    public T Data { get; set; } = default!;
}
