using Asgard.Brigitte.Desktop.Core;
namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>验证本地日期与 UTC 边界，防止午夜、夏令时和结束日漏数据。</summary>
public sealed class LocalDateRangeTests
{
    [Fact]
    public void Shanghai_day_includes_last_tick_and_excludes_next_midnight()
    {
        var range = LocalDateRange.Create(new(2026, 10, 20), new(2026, 10, 20), TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"));
        Assert.Equal(DateTimeOffset.Parse("2026-10-19T16:00:00Z"), range.FromUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T16:00:00Z"), range.UntilUtc);
        Assert.False(range.Contains(range.FromUtc!.Value.AddTicks(-1)));
        Assert.True(range.Contains(range.FromUtc.Value));
        Assert.True(range.Contains(range.UntilUtc!.Value.AddTicks(-1)));
        Assert.False(range.Contains(range.UntilUtc.Value));
    }
    [Theory]
    [InlineData(3, 8, 23)]
    [InlineData(11, 1, 25)]
    public void Daylight_saving_days_use_real_local_midnights(int month, int day, int hours)
    {
        var range = LocalDateRange.Create(new(2026, month, day), new(2026, month, day), TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        Assert.Equal(hours, (range.UntilUtc!.Value - range.FromUtc!.Value).TotalHours);
    }
    [Fact]
    public void Open_bounds_and_invalid_order_are_explicit()
    {
        Assert.True(LocalDateRange.Create(null, null).Contains(DateTimeOffset.MinValue));
        Assert.Null(LocalDateRange.Create(null, new(2026, 10, 20), TimeZoneInfo.Utc).FromUtc);
        Assert.Null(LocalDateRange.Create(new(2026, 10, 20), null, TimeZoneInfo.Utc).UntilUtc);
        Assert.Throws<ArgumentException>(() => LocalDateRange.Create(new(2026, 10, 21), new(2026, 10, 20)));
    }
}
