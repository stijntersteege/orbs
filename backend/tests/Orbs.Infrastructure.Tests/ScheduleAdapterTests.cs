using Orbs.Domain.Common;
using Orbs.Infrastructure.Schedules;

namespace Orbs.Infrastructure.Tests;

public class ScheduleAdapterTests
{
    private readonly CronCalculator _cron = new();

    [Fact]
    public void Cron_follows_the_time_zone_including_summer_time()
    {
        var winter = _cron.NextAfter("0 9 * * 1", "Europe/Amsterdam", new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
        var summer = _cron.NextAfter("0 9 * * 1", "Europe/Amsterdam", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        winter.ShouldBe(new DateTimeOffset(2026, 12, 7, 8, 0, 0, TimeSpan.Zero));
        summer.ShouldBe(new DateTimeOffset(2026, 7, 6, 7, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("every monday", "UTC")]
    [InlineData("0 9 * * 1", "Mars/Olympus")]
    public void Bad_expressions_and_zones_are_explained(string expression, string zone)
    {
        Should.Throw<DomainException>(() => _cron.Validate(expression, zone));
    }

    [Fact]
    public void Rss_items_are_read_with_their_guid()
    {
        var items = FeedReader.Parse("""
            <rss version="2.0"><channel><title>Blog</title>
              <item><title>Second</title><link>https://b.example/2</link><guid>post-2</guid><description>&lt;p&gt;Hello &lt;b&gt;world&lt;/b&gt;&lt;/p&gt;</description></item>
              <item><title>First</title><link>https://b.example/1</link></item>
            </channel></rss>
            """);

        items.Select(i => i.Id).ShouldBe(["post-2", "https://b.example/1"]);
        items[0].Summary.ShouldBe("Hello world");
    }

    [Fact]
    public void Atom_entries_are_read_too()
    {
        var items = FeedReader.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"><title>News</title>
              <entry><id>urn:1</id><title>Launch</title><link rel="alternate" href="https://n.example/launch"/><summary>Out now</summary></entry>
            </feed>
            """);

        var item = items.ShouldHaveSingleItem();
        item.ShouldBe(new Orbs.Application.Schedules.FeedItem("urn:1", "Launch", "https://n.example/launch", "Out now"));
    }
}
