using Orbs.Domain.Common;

namespace Orbs.Domain.Users;

/// <summary>
/// A browser or device that agreed to receive notifications (Web Push). The keys encrypt each message
/// so that only that browser can read it.
/// </summary>
public sealed class PushSubscription : OwnedAggregateRoot
{
    public const int MaxEndpointLength = 2_048;

    private PushSubscription(Guid id, string endpoint, string p256dh, string auth, string device, DateTimeOffset now) : base(id, now)
    {
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        Device = device;
    }

    // For EF Core materialization.
    private PushSubscription()
    {
        Endpoint = string.Empty;
        P256dh = string.Empty;
        Auth = string.Empty;
        Device = string.Empty;
    }

    /// <summary>The push service URL of this browser.</summary>
    public string Endpoint { get; private set; }

    public string P256dh { get; private set; }

    public string Auth { get; private set; }

    public string Device { get; private set; }

    public static PushSubscription Create(string endpoint, string p256dh, string auth, string? device, DateTimeOffset now)
    {
        var uri = Guard.AbsoluteHttpUri(endpoint, nameof(Endpoint)) ?? throw new DomainException("Endpoint is required.");
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new DomainException("Push endpoints use https.");
        }

        var text = (device ?? string.Empty).Trim();
        return new PushSubscription(
            Guid.NewGuid(),
            Guard.Text(uri.ToString(), nameof(Endpoint), MaxEndpointLength),
            Guard.Text(p256dh, nameof(P256dh), 200),
            Guard.Text(auth, nameof(Auth), 100),
            text.Length <= 200 ? text : text[..200],
            now);
    }
}

/// <summary>When someone does not want to be disturbed: a daily window in their time zone.</summary>
public sealed record QuietHours(TimeOnly From, TimeOnly Until, string TimeZone)
{
    /// <summary>True when <paramref name="now"/> falls in the window. A window may run past midnight.</summary>
    public bool Contains(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return From <= Until ? local >= From && local < Until : local >= From || local < Until;
    }

    /// <summary>The next moment the window ends, at or after <paramref name="now"/>.</summary>
    public DateTimeOffset EndsAfter(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var end = new DateTime(DateOnly.FromDateTime(local.DateTime), Until, DateTimeKind.Unspecified);
        if (end <= local.DateTime)
        {
            end = end.AddDays(1);
        }

        return new DateTimeOffset(end, zone.GetUtcOffset(end));
    }
}
