using Orbs.Domain.Common;

namespace Orbs.Domain.Schedules;

/// <summary>What makes a schedule run.</summary>
public enum TriggerKind
{
    /// <summary>A cron expression in a time zone, such as "every Monday at 9:00".</summary>
    Time,

    /// <summary>Another app calls a secret URL.</summary>
    Webhook,

    /// <summary>A new item appears in an RSS or Atom feed.</summary>
    Rss,

    /// <summary>New email arrives in a connected email account.</summary>
    Email,
}

/// <summary>
/// Work an orb does on its own: when the trigger fires, the orb gets <see cref="Prompt"/> (plus what triggered it,
/// such as new feed items) in the schedule's own chat, and answers like it would to the person.
/// </summary>
public sealed class Schedule : OwnedAggregateRoot
{
    public const int MaxNameLength = 80;
    public const int MaxPromptLength = 4_000;
    public const int MaxLastSeenLength = 4_000;

    /// <summary>Feeds and inboxes are checked this often.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private Schedule(Guid id, Guid orbId, string name, string prompt, TriggerKind trigger, DateTimeOffset now) : base(id, now)
    {
        OrbId = orbId;
        Name = name;
        Prompt = prompt;
        Trigger = trigger;
    }

    // For EF Core materialization.
    private Schedule()
    {
        Name = string.Empty;
        Prompt = string.Empty;
    }

    public Guid OrbId { get; private set; }

    public string Name { get; private set; }

    /// <summary>What the orb is asked each time, such as "Summarize my week".</summary>
    public string Prompt { get; private set; }

    public TriggerKind Trigger { get; private set; }

    /// <summary>For <see cref="TriggerKind.Time"/>: a five-field cron expression, such as "0 9 * * 1".</summary>
    public string? Cron { get; private set; }

    /// <summary>For <see cref="TriggerKind.Time"/>: an IANA time zone, such as Europe/Amsterdam.</summary>
    public string TimeZone { get; private set; } = "UTC";

    /// <summary>For <see cref="TriggerKind.Rss"/>: the feed address.</summary>
    public string? FeedUrl { get; private set; }

    /// <summary>For <see cref="TriggerKind.Email"/>: the connected email account.</summary>
    public Guid? IntegrationId { get; private set; }

    /// <summary>For <see cref="TriggerKind.Webhook"/>: a hash of the secret in the hook URL.</summary>
    public string? WebhookSecretHash { get; private set; }

    /// <summary>What the last check saw (feed item ids, message ids), so only new things trigger a run.</summary>
    public string? LastSeen { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    /// <summary>When the dispatcher looks at this schedule next. Null for webhooks, which run when called.</summary>
    public DateTimeOffset? NextCheckAt { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    /// <summary>The chat the orb writes into. Created on the first run.</summary>
    public Guid? ConversationId { get; private set; }

    public static Schedule Create(Guid orbId, string name, string prompt, TriggerKind trigger, DateTimeOffset now)
    {
        Guard.NotEmpty(orbId, nameof(orbId));
        return new Schedule(Guid.NewGuid(), orbId, Guard.Text(name, nameof(Name), MaxNameLength), Guard.Text(prompt, nameof(Prompt), MaxPromptLength), trigger, now);
    }

    public void Describe(string name, string prompt, DateTimeOffset now)
    {
        Name = Guard.Text(name, nameof(Name), MaxNameLength);
        Prompt = Guard.Text(prompt, nameof(Prompt), MaxPromptLength);
        Touch(now);
    }

    public void RunAt(string cron, string? timeZone, DateTimeOffset firstCheck, DateTimeOffset now)
    {
        Trigger = TriggerKind.Time;
        Cron = Guard.Text(cron, nameof(Cron), 100);
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : Guard.Text(timeZone, nameof(TimeZone), 100);
        NextCheckAt = firstCheck;
        Touch(now);
    }

    public void WatchFeed(string feedUrl, DateTimeOffset now)
    {
        Trigger = TriggerKind.Rss;
        FeedUrl = Guard.AbsoluteHttpUri(feedUrl, nameof(FeedUrl))?.ToString() ?? throw new DomainException("FeedUrl is required.");
        NextCheckAt = now;
        Touch(now);
    }

    public void WatchEmail(Guid integrationId, DateTimeOffset now)
    {
        Guard.NotEmpty(integrationId, nameof(integrationId));
        Trigger = TriggerKind.Email;
        IntegrationId = integrationId;
        NextCheckAt = now;
        Touch(now);
    }

    public void ListenForWebhook(string secretHash, DateTimeOffset now)
    {
        Trigger = TriggerKind.Webhook;
        WebhookSecretHash = Guard.Text(secretHash, nameof(secretHash), 128);
        NextCheckAt = null;
        Touch(now);
    }

    public void Enable(bool enabled, DateTimeOffset now)
    {
        IsEnabled = enabled;
        Touch(now);
    }

    /// <summary>Records a check. <paramref name="next"/> is when to look again; null stops checking.</summary>
    public void Checked(DateTimeOffset? next, string? lastSeen, DateTimeOffset now)
    {
        NextCheckAt = next;
        if (lastSeen is not null)
        {
            LastSeen = lastSeen.Length <= MaxLastSeenLength ? lastSeen : lastSeen[..MaxLastSeenLength];
        }

        Touch(now);
    }

    public void Ran(Guid conversationId, DateTimeOffset now)
    {
        Guard.NotEmpty(conversationId, nameof(conversationId));
        ConversationId = conversationId;
        LastRunAt = now;
        Touch(now);
    }

    public bool IsDue(DateTimeOffset now) => IsEnabled && NextCheckAt is { } next && next <= now;
}
