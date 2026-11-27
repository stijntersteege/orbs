using Orbs.Application.Schedules;

namespace Orbs.Testing;

/// <summary>Real time, plus however far a test moved it forward. Keeps ordering natural while letting schedules fall due.</summary>
public sealed class TestClock : TimeProvider
{
    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + _offset;

    public void Advance(TimeSpan by) => _offset += by;
}

/// <summary>A feed whose items a test sets. Newest first, like real feeds.</summary>
public sealed class FakeFeedReader : IFeedReader
{
    public List<FeedItem> Items { get; } = [];

    public Task<IReadOnlyList<FeedItem>> ReadAsync(string url, bool restrictNetwork, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FeedItem>>([.. Items]);
}
