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
/// Memories go into the orb's prompt, and the person can read, edit and forget every one of them.
/// </summary>
public sealed class Memory : OwnedAggregateRoot
{
    public const int MaxContentLength = 1_000;

    private Memory(Guid id, Guid orbId, string content, MemorySource source, Guid? conversationId, DateTimeOffset now) : base(id, now)
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

    /// <summary>The orb that remembers this.</summary>
    public Guid OrbId { get; private set; }

    public string Content { get; private set; }

    public MemorySource Source { get; private set; }

    /// <summary>The chat a learned memory came from, if any.</summary>
    public Guid? ConversationId { get; private set; }

    public static Memory Create(Guid orbId, string content, MemorySource source, DateTimeOffset now, Guid? conversationId = null)
    {
        Guard.NotEmpty(orbId, nameof(orbId));
        return new Memory(Guid.NewGuid(), orbId, Guard.Text(content, nameof(Content), MaxContentLength), source, conversationId, now);
    }

    public void Edit(string content, DateTimeOffset now)
    {
        Content = Guard.Text(content, nameof(Content), MaxContentLength);
        Touch(now);
    }
}
