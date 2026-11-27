using Orbs.Domain.Users;

namespace Orbs.Domain.Tests;

public class QuietHoursTests
{
    private static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
    private readonly QuietHours _night = new(new TimeOnly(22, 0), new TimeOnly(7, 30), "Europe/Amsterdam");

    [Theory]
    [InlineData(21, 59, false)]
    [InlineData(22, 0, true)]
    [InlineData(2, 0, true)]
    [InlineData(7, 29, true)]
    [InlineData(7, 30, false)]
    [InlineData(12, 0, false)]
    public void A_window_can_run_past_midnight(int hour, int minute, bool quiet)
    {
        var local = new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(1));

        _night.Contains(local, Amsterdam).ShouldBe(quiet);
    }

    [Fact]
    public void Quiet_hours_end_the_next_morning()
    {
        var lateEvening = new DateTimeOffset(2026, 12, 1, 23, 0, 0, TimeSpan.FromHours(1));

        _night.EndsAfter(lateEvening, Amsterdam).ShouldBe(new DateTimeOffset(2026, 12, 2, 7, 30, 0, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Push_endpoints_must_use_https()
    {
        Should.Throw<global::Orbs.Domain.Common.DomainException>(() => PushSubscription.Create("http://push.example/abc", "k", "a", null, DateTimeOffset.UnixEpoch));
    }
}
