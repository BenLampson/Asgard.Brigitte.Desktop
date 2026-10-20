namespace Asgard.Brigitte.Desktop.Core;

/// <summary>按设备时区选择完整日期，UTC 数据保持原样；上界使用次日零点避免精度遗漏。</summary>
public sealed record LocalDateRange(DateTimeOffset? FromUtc, DateTimeOffset? UntilUtc)
{
    /// <summary>显式转换两个边界，夏令时切换日可能并非二十四小时。</summary>
    public static LocalDateRange Create(DateTime? start, DateTime? end, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        if (start.HasValue && end.HasValue && start.Value.Date > end.Value.Date)
            throw new ArgumentException("开始日期不能晚于结束日期。");
        DateTimeOffset? Boundary(DateTime? value)
        {
            if (value is null) return null;
            var local = DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local)) throw new ArgumentException("所选日期的零点在本地时区不存在，请调整日期。");
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
        }
        return new(Boundary(start), Boundary(end?.Date.AddDays(1)));
    }

    /// <summary>同一范围用于本机模式和同步缓存，不触发服务器请求。</summary>
    public bool Contains(DateTimeOffset value) => (!FromUtc.HasValue || value >= FromUtc.Value)
        && (!UntilUtc.HasValue || value < UntilUtc.Value);
}
