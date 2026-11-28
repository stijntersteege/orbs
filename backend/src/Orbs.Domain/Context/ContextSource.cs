using Orbs.Domain.Common;

namespace Orbs.Domain.Context;

public enum ContextSourceStatus { Pending, Ready, Failed }

/// <summary>How often a source reads itself again.</summary>
public enum SyncInterval { Off, Hourly, Daily, Weekly }

/// <summary>
/// Something orbs can know: a note, a web page, an API, a repository.
/// Connected once, then shared with the orbs the user picks.
/// <see cref="Kind"/> names the connector that reads it.
/// </summary>
public sealed class ContextSource : OwnedAggregateRoot
{
    public const int MaxNameLength = 80;
    public const int MaxLocationLength = 2_048;
    public const int MaxContentLength = 200_000;

    private ContextSource(Guid id, string name, string kind, DateTimeOffset now) : base(id, now)
    {
        Name = name;
        Kind = kind;
    }

    // For EF Core materialization.
    private ContextSource()
    {
        Name = string.Empty;
        Kind = string.Empty;
    }

    public string Name { get; private set; }

    /// <summary>Connector key, for example <c>text</c> or <c>url</c>.</summary>
    public string Kind { get; private set; }

    /// <summary>Where the connector reads from: a URL, a path. Empty for inline kinds.</summary>
    public string Location { get; private set; } = string.Empty;

    /// <summary>Raw input for inline kinds, such as the text of a note.</summary>
    public string Input { get; private set; } = string.Empty;

    /// <summary>The text orbs get to see, as last synced.</summary>
    public string Content { get; private set; } = string.Empty;

    public ContextSourceStatus Status { get; private set; } = ContextSourceStatus.Pending;

    public string? LastError { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>How many passages of the current content are embedded for retrieval. Zero until indexed.</summary>
    public int IndexedChunks { get; private set; }

    public DateTimeOffset? IndexedAt { get; private set; }

    /// <summary>Keeps the source up to date in the background. Off reads it only when asked.</summary>
    public SyncInterval SyncEvery { get; private set; } = SyncInterval.Off;

    /// <summary>When the next background sync is due, or null when it is off.</summary>
    public DateTimeOffset? NextSyncAt { get; private set; }

    public void KeepInSync(SyncInterval every, DateTimeOffset now)
    {
        SyncEvery = every;
        NextSyncAt = Next(every, now);
        Touch(now);
    }

    /// <summary>Plans the next background sync after one ran (or was queued).</summary>
    public void SyncPlanned(DateTimeOffset now)
    {
        NextSyncAt = Next(SyncEvery, now);
        Touch(now);
    }

    private static DateTimeOffset? Next(SyncInterval every, DateTimeOffset now) => every switch
    {
        SyncInterval.Hourly => now.AddHours(1),
        SyncInterval.Daily => now.AddDays(1),
        SyncInterval.Weekly => now.AddDays(7),
        _ => null,
    };

    public static ContextSource Create(string name, string kind, DateTimeOffset now, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guard.Text(name, nameof(Name), MaxNameLength), Slug.Normalize(kind, nameof(Kind)), now);

    public void Rename(string name, DateTimeOffset now)
    {
        Name = Guard.Text(name, nameof(Name), MaxNameLength);
        Touch(now);
    }

    /// <summary>Changes what the connector reads. The source needs a new sync afterwards.</summary>
    public void Configure(string? location, string? input, DateTimeOffset now)
    {
        Location = Guard.Text(location, nameof(Location), MaxLocationLength, required: false);
        Input = Guard.Text(input, nameof(Input), MaxContentLength, required: false);
        Status = ContextSourceStatus.Pending;
        Touch(now);
    }

    /// <summary>Records a successful sync. Content past the limit is cut, never rejected.</summary>
    public void MarkSynced(string content, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content.Length > MaxContentLength ? content[..MaxContentLength] : content;
        Status = ContextSourceStatus.Ready;
        LastError = null;
        LastSyncedAt = now;
        IndexedChunks = 0; // The content may have changed; the old passages no longer count.
        Touch(now);
    }

    public void MarkIndexed(int chunks, DateTimeOffset now)
    {
        IndexedChunks = Math.Max(0, chunks);
        IndexedAt = now;
        Touch(now);
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        var message = string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim();
        Status = ContextSourceStatus.Failed;
        LastError = message.Length > 1_000 ? message[..1_000] : message;
        Touch(now);
    }
}
