using Orbs.Domain.Common;

namespace Orbs.Domain.Memories;

/// <summary>Where a memory came from.</summary>
public enum MemorySource
{
    /// <summary>The person wrote it in the Memory tab.</summary>
    Manual,

    /// <summary>The orb saved it during a chat.</summary>
    Learned,
}

/// <summary>
/// Something an orb remembers about the person or their world, such as "Sam prefers mornings for meetings".
/// A memory is private to one orb, or shared with all of the person's orbs (no <see cref="OrbId"/>).
/// Memories go into the prompt, and the person can read, edit, share and forget every one of them.
/// </summary>
public sealed class Memory : OwnedAggregateRoot
{
    public const int MaxContentLength = 1_000;

    private Memory(Guid id, Guid? orbId, string content, MemorySource source, Guid? conversationId, DateTimeOffset now) : base(id, now)
    {
        OrbId = orbId;
        Content = content;
        Source = source;
        ConversationId = conversationId;
    }

    // For EF Core materialization.
    private Memory()
    {
        Content = string.Empty;
    }

    /// <summary>The orb that remembers this, or <c>null</c> when every orb does.</summary>
    public Guid? OrbId { get; private set; }

    public bool IsShared => OrbId is null;

    public string Content { get; private set; }

    public MemorySource Source { get; private set; }

    /// <summary>The chat a learned memory came from, if any.</summary>
    public Guid? ConversationId { get; private set; }

    /// <summary>A memory for one orb, or for all orbs when <paramref name="orbId"/> is null.</summary>
    public static Memory Create(Guid? orbId, string content, MemorySource source, DateTimeOffset now, Guid? conversationId = null)
    {
        if (orbId is { } id)
        {
            Guard.NotEmpty(id, nameof(orbId));
        }

        return new Memory(Guid.NewGuid(), orbId, Guard.Text(content, nameof(Content), MaxContentLength), source, conversationId, now);
    }

    public void Edit(string content, DateTimeOffset now)
    {
        Content = Guard.Text(content, nameof(Content), MaxContentLength);
        Touch(now);
    }

    /// <summary>Every orb remembers this from now on.</summary>
    public void Share(DateTimeOffset now)
    {
        OrbId = null;
        Touch(now);
    }

    /// <summary>Only <paramref name="orbId"/> remembers this from now on.</summary>
    public void KeepFor(Guid orbId, DateTimeOffset now)
    {
        Guard.NotEmpty(orbId, nameof(orbId));
        OrbId = orbId;
        Touch(now);
    }
}
