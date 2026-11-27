using System.Text.Json;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Jobs;
using Orbs.Domain.Common;
using Orbs.Domain.Jobs;
using Orbs.Domain.Users;

namespace Orbs.Application.Notifications;

public interface IPushSubscriptionRepository : IRepository<PushSubscription>
{
    Task<PushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>One user's devices. Works as the system too, for background work.</summary>
    Task<IReadOnlyList<PushSubscription>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public enum PushResult { Delivered, Gone, Failed }

/// <summary>Sends Web Push messages, signed with this server's VAPID keys.</summary>
public interface IPushSender
{
    string PublicKey { get; }

    Task<PushResult> SendAsync(PushSubscription subscription, string payloadJson, CancellationToken cancellationToken = default);
}

/// <summary>What a notification shows and where tapping it goes.</summary>
public sealed record Notification(string Title, string Body, string? Url = null);

public sealed record SubscribeRequest(string Endpoint, PushKeys Keys);

public sealed record PushKeys(string P256dh, string Auth);

/// <summary>Quiet hours as "HH:mm" in an IANA time zone. Both times null means no quiet hours.</summary>
public sealed record NotificationSettingsDto(string? QuietFrom, string? QuietUntil, string TimeZone, int Devices);

public sealed record SaveNotificationSettingsRequest(string? QuietFrom, string? QuietUntil, string? TimeZone);

/// <summary>Reaches people on their devices, holding messages back during their quiet hours.</summary>
public sealed class NotificationService(
    IPushSubscriptionRepository subscriptions,
    IUserRepository users,
    IPushSender sender,
    JobScheduler jobs,
    CurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public string PublicKey => sender.PublicKey;

    public async Task SubscribeAsync(SubscribeRequest request, string? device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Keys);
        currentUser.RequireUserId();
        if (await subscriptions.FindByEndpointAsync(request.Endpoint, cancellationToken) is { } existing)
        {
            subscriptions.Remove(existing);
        }

        subscriptions.Add(PushSubscription.Create(request.Endpoint, request.Keys.P256dh, request.Keys.Auth, device, clock.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsubscribeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (await subscriptions.FindByEndpointAsync(endpoint, cancellationToken) is { } existing)
        {
            subscriptions.Remove(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<NotificationSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(cancellationToken);
        var devices = (await subscriptions.ListForUserAsync(user.Id, cancellationToken)).Count;
        return ToDto(user.QuietHours, devices);
    }

    public async Task<NotificationSettingsDto> SaveSettingsAsync(SaveNotificationSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await LoadUserAsync(cancellationToken);
        QuietHours? quiet = null;
        if (!string.IsNullOrWhiteSpace(request.QuietFrom) || !string.IsNullOrWhiteSpace(request.QuietUntil))
        {
            var zone = string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone.Trim();
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
            {
                throw new DomainException($"Unknown time zone '{zone}'.");
            }

            quiet = new QuietHours(ParseTime(request.QuietFrom), ParseTime(request.QuietUntil), zone);
        }

        user.SetQuietHours(quiet, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(quiet, (await subscriptions.ListForUserAsync(user.Id, cancellationToken)).Count);
    }

    /// <summary>
    /// Notifies a user on every device. During quiet hours the message waits in the job queue until they end.
    /// Returns how many devices got it now (0 when held back).
    /// </summary>
    public async Task<int> NotifyAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        if (user.QuietHours is { } quiet && TimeZoneInfo.TryFindSystemTimeZoneById(quiet.TimeZone, out var zone) && quiet.Contains(now, zone))
        {
            await jobs.EnqueueAsync(NotificationJob.KindName, new NotificationJob.Payload(userId, notification), quiet.EndsAfter(now, zone), maxAttempts: 1, ownerId: userId, cancellationToken: cancellationToken);
            return 0;
        }

        return await SendNowAsync(userId, notification, cancellationToken);
    }

    /// <summary>Sends right away, ignoring quiet hours, and forgets devices that unsubscribed.</summary>
    public async Task<int> SendNowAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var payload = JsonSerializer.Serialize(new { title = notification.Title, body = notification.Body, url = notification.Url ?? "/" });
        var delivered = 0;
        foreach (var subscription in await subscriptions.ListForUserAsync(userId, cancellationToken))
        {
            switch (await sender.SendAsync(subscription, payload, cancellationToken))
            {
                case PushResult.Delivered:
                    delivered++;
                    break;
                case PushResult.Gone:
                    subscriptions.Remove(subscription);
                    break;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return delivered;
    }

    /// <summary>Sends a test notification to the signed-in user's devices.</summary>
    public async Task<int> TestAsync(CancellationToken cancellationToken = default) =>
        await SendNowAsync(currentUser.RequireUserId(), new Notification("Orbs", "Notifications work on this device."), cancellationToken);

    private static TimeOnly ParseTime(string? value) =>
        TimeOnly.TryParseExact(value?.Trim(), "HH:mm", out var time) ? time : throw new DomainException("Times are written as HH:mm, such as 22:00.");

    private static NotificationSettingsDto ToDto(QuietHours? quiet, int devices) =>
        new(quiet?.From.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), quiet?.Until.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), quiet?.TimeZone ?? "UTC", devices);

    private async Task<User> LoadUserAsync(CancellationToken cancellationToken) =>
        await users.FindAsync(currentUser.RequireUserId(), cancellationToken) ?? throw new UnauthorizedException();
}

/// <summary>Delivers a notification that waited for quiet hours to end.</summary>
public sealed class NotificationJob(NotificationService notifications) : IJobHandler
{
    public const string KindName = "notification.send";

    public string Kind => KindName;

    public sealed record Payload(Guid UserId, Notification Notification);

    public async Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobScheduler.ReadPayload<Payload>(job);
        var delivered = await notifications.SendNowAsync(payload.UserId, payload.Notification, cancellationToken);
        return $"Delivered to {delivered} device(s).";
    }
}
