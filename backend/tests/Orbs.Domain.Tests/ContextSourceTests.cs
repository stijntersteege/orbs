using Orbs.Domain.Context;

namespace Orbs.Domain.Tests;

public class ContextSourceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void A_new_source_waits_for_its_first_sync()
    {
        var source = ContextSource.Create("Roadmap", "url", Now);

        source.Status.ShouldBe(ContextSourceStatus.Pending);
        source.LastSyncedAt.ShouldBeNull();
    }

    [Fact]
    public void MarkSynced_stores_content_and_clears_errors()
    {
        var source = ContextSource.Create("Roadmap", "url", Now);
        source.MarkFailed("timeout", Now);

        source.MarkSynced("Q1: ship it", Now);

        source.Status.ShouldBe(ContextSourceStatus.Ready);
        source.Content.ShouldBe("Q1: ship it");
        source.LastError.ShouldBeNull();
        source.LastSyncedAt.ShouldBe(Now);
    }

    [Fact]
    public void MarkSynced_cuts_content_that_is_too_long()
    {
        var source = ContextSource.Create("Big", "text", Now);

        source.MarkSynced(new string('a', ContextSource.MaxContentLength + 10), Now);

        source.Content.Length.ShouldBe(ContextSource.MaxContentLength);
    }

    [Fact]
    public void Reconfiguring_a_source_asks_for_a_new_sync()
    {
        var source = ContextSource.Create("Docs", "url", Now);
        source.MarkSynced("old", Now);

        source.Configure("https://example.com/new", null, Now);

        source.Status.ShouldBe(ContextSourceStatus.Pending);
        source.Location.ShouldBe("https://example.com/new");
    }

    [Fact]
    public void MarkFailed_keeps_a_readable_error()
    {
        var source = ContextSource.Create("Docs", "url", Now);

        source.MarkFailed("", Now);

        source.Status.ShouldBe(ContextSourceStatus.Failed);
        source.LastError.ShouldBe("Unknown error.");
    }
}
