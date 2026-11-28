using Orbs.Domain.Common;

namespace Orbs.Domain.Users;

/// <summary>
/// A signed-in device. The client holds the token; the server keeps only its hash.
/// Sessions slide: each use pushes the expiry out to <see cref="Lifetime"/> after the last use.
/// </summary>
public sealed class Session : AggregateRoot
{
    public const int MaxDescriptionLength = 200;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SeenGranularity = TimeSpan.FromMinutes(5);

    private Session(Guid id, Guid userId, string tokenHash, string description, DateTimeOffset now) : base(id, now)
    {
        UserId = userId;
        TokenHash = tokenHash;
        Description = description;
        LastSeenAt = now;
        ExpiresAt = now + Lifetime;
    }

    // For EF Core materialization.
    private Session()
    {
        TokenHash = string.Empty;
        Description = string.Empty;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    /// <summary>Which device or browser, taken from its user agent.</summary>
    public string Description { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static Session Start(Guid userId, string tokenHash, string? description, DateTimeOffset now)
    {
        Guard.NotEmpty(userId, nameof(userId));
        var text = (description ?? string.Empty).Trim();
        if (text.Length > MaxDescriptionLength)
        {
            text = text[..MaxDescriptionLength];
        }

        return new Session(Guid.NewGuid(), userId, Guard.Text(tokenHash, nameof(tokenHash), 128), text, now);
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Records a use. Returns true when it changed anything worth saving (at most every few minutes).</summary>
    public bool Seen(DateTimeOffset now)
    {
        if (now - LastSeenAt < SeenGranularity)
        {
            return false;
        }

        LastSeenAt = now;
        ExpiresAt = now + Lifetime;
        Touch(now);
        return true;
    }
}
