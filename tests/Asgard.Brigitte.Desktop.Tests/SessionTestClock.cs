namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>只推进测试时间，不等待真实令牌过期。</summary>
internal sealed class SessionTestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    internal void Advance(TimeSpan delta) => _now += delta;
}
