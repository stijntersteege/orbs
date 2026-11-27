using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Common;
using Orbs.Application.Conversations;
using Orbs.Application.Jobs;
using Orbs.Application.Tools;
using Orbs.Domain.Common;
using Orbs.Domain.Jobs;
using Orbs.Domain.Schedules;

namespace Orbs.Application.Schedules;

public interface IScheduleRepository : IRepository<Schedule>
{
    /// <summary>Schedules whose next check has come, across all users. Called as the system.</summary>
    Task<IReadOnlyList<Schedule>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);
}

/// <summary>Works out when a cron expression fires next.</summary>
public interface ICronCalculator
{
    /// <summary>Throws a <see cref="DomainException"/> for an expression or time zone it does not understand.</summary>
    void Validate(string expression, string timeZone);

    DateTimeOffset? NextAfter(string expression, string timeZone, DateTimeOffset after);
}

/// <summary>An item in an RSS or Atom feed.</summary>
public sealed record FeedItem(string Id, string Title, string? Link, string? Summary);

public interface IFeedReader
{
    /// <summary>The feed's items, newest first. <paramref name="restrictNetwork"/> applies the network policy.</summary>
    Task<IReadOnlyList<FeedItem>> ReadAsync(string url, bool restrictNetwork, CancellationToken cancellationToken = default);
}

public sealed record ScheduleDto(
    Guid Id,
    Guid OrbId,
    string Name,
    string Prompt,
    TriggerKind Trigger,
    string? Cron,
    string TimeZone,
    string? FeedUrl,
    Guid? IntegrationId,
    bool IsEnabled,
    DateTimeOffset? NextCheckAt,
    DateTimeOffset? LastRunAt,
    Guid? ConversationId,
    DateTimeOffset CreatedAt,
    string? WebhookPath = null);

/// <summary>Creates or changes a schedule. Which trigger fields count depends on <see cref="Trigger"/>.</summary>
public sealed record SaveScheduleRequest(
    Guid OrbId,
    string Name,
    string Prompt,
    TriggerKind Trigger,
    string? Cron = null,
    string? TimeZone = null,
    string? FeedUrl = null,
    Guid? IntegrationId = null,
    bool IsEnabled = true);

/// <summary>Use cases for an orb's schedules and triggers.</summary>
public sealed class ScheduleService(
    IScheduleRepository schedules,
    IOrbRepository orbs,
    IIntegrationRepository integrations,
    ICronCalculator cron,
    JobScheduler jobs,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<ScheduleDto>> ListAsync(Guid? orbId, CancellationToken cancellationToken = default) =>
        [.. (await schedules.ListAsync(cancellationToken)).Where(s => orbId is null || s.OrbId == orbId).OrderBy(s => s.CreatedAt).Select(s => s.ToDto())];

    /// <summary>Creates a schedule. For webhooks the answer holds the secret path once; it is never shown again.</summary>
    public async Task<ScheduleDto> CreateAsync(SaveScheduleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureOrbAsync(request.OrbId, cancellationToken);
        var now = clock.GetUtcNow();
        var schedule = Schedule.Create(request.OrbId, request.Name, request.Prompt, request.Trigger, now);
        var secret = await ApplyTriggerAsync(schedule, request, null, cancellationToken);
        schedule.Enable(request.IsEnabled, now);
        schedules.Add(schedule);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return schedule.ToDto() with { WebhookPath = secret is null ? null : WebhookPath(schedule.Id, secret) };
    }

    public async Task<ScheduleDto> UpdateAsync(Guid id, SaveScheduleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var schedule = await LoadAsync(id, cancellationToken);
        var now = clock.GetUtcNow();
        schedule.Describe(request.Name, request.Prompt, now);
        var secret = await ApplyTriggerAsync(schedule, request, schedule, cancellationToken);
        schedule.Enable(request.IsEnabled, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return schedule.ToDto() with { WebhookPath = secret is null ? null : WebhookPath(schedule.Id, secret) };
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        schedules.Remove(await LoadAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Runs the schedule now, in the background, whatever its trigger.</summary>
    public async Task RunNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await LoadAsync(id, cancellationToken);
        await jobs.EnqueueAsync(ScheduleRunJob.KindName, new ScheduleRunJob.Payload(schedule.Id, null), maxAttempts: 1, ownerId: schedule.OwnerId, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Called by another app on the secret hook URL. Must run as the system: nobody is signed in.
    /// Returns false for an unknown schedule or a wrong secret, without saying which.
    /// </summary>
    public async Task<bool> ReceiveWebhookAsync(Guid id, string secret, string? body, CancellationToken cancellationToken = default)
    {
        var schedule = await schedules.FindAsync(id, cancellationToken);
        if (schedule is not { Trigger: TriggerKind.Webhook, IsEnabled: true } || schedule.WebhookSecretHash is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret ?? string.Empty)), Encoding.ASCII.GetBytes(schedule.WebhookSecretHash)))
        {
            return false;
        }

        var input = string.IsNullOrWhiteSpace(body) ? null : body.Length <= 10_000 ? body : body[..10_000];
        await jobs.EnqueueAsync(ScheduleRunJob.KindName, new ScheduleRunJob.Payload(schedule.Id, input), maxAttempts: 1, ownerId: schedule.OwnerId, cancellationToken: cancellationToken);
        return true;
    }

    public static string WebhookPath(Guid id, string secret) => $"/api/hooks/{id}/{secret}";

    internal static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>Sets the trigger. Returns a new webhook secret when one was made.</summary>
    private async Task<string?> ApplyTriggerAsync(Schedule schedule, SaveScheduleRequest request, Schedule? existing, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        switch (request.Trigger)
        {
            case TriggerKind.Time:
                var expression = request.Cron ?? throw new DomainException("Say when it runs, as a cron expression such as \"0 9 * * 1\".");
                var zone = string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone;
                cron.Validate(expression, zone);
                schedule.RunAt(expression, zone, cron.NextAfter(expression, zone, now) ?? throw new DomainException("This schedule never runs."), now);
                return null;
            case TriggerKind.Rss:
                schedule.WatchFeed(request.FeedUrl ?? throw new DomainException("Give the feed address."), now);
                return null;
            case TriggerKind.Email:
                var integrationId = request.IntegrationId ?? throw new DomainException("Choose the email account to watch.");
                var integration = await integrations.FindAsync(integrationId, cancellationToken);
                if (integration is not { Kind: "email" })
                {
                    throw new DomainException("That is not one of your email accounts.");
                }

                schedule.WatchEmail(integrationId, now);
                return null;
            case TriggerKind.Webhook:
                if (existing is { Trigger: TriggerKind.Webhook, WebhookSecretHash: not null })
                {
                    return null; // Keep the URL people already use.
                }

                var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
                schedule.ListenForWebhook(Hash(secret), now);
                return secret;
            default:
                throw new DomainException($"Unknown trigger {request.Trigger}.");
        }
    }

    private async Task EnsureOrbAsync(Guid orbId, CancellationToken cancellationToken) =>
        _ = await orbs.FindAsync(orbId, cancellationToken) ?? throw new NotFoundException("Orb", orbId);

    private async Task<Schedule> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await schedules.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Schedule", id);
}

/// <summary>
/// Finds schedules whose check is due (as the system) and queues a check for each, owned by its owner.
/// Called by the job worker every few seconds.
/// </summary>
public sealed class ScheduleDispatcher(IScheduleRepository schedules, JobScheduler jobs, ICronCalculator cron, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var due = await schedules.ListDueAsync(now, 50, cancellationToken);
        foreach (var schedule in due)
        {
            // Move the next check first, so a slow or failing check never piles up.
            var next = schedule.Trigger == TriggerKind.Time
                ? cron.NextAfter(schedule.Cron!, schedule.TimeZone, now)
                : now + Schedule.PollInterval;
            schedule.Checked(next, null, now);
            await jobs.EnqueueAsync(ScheduleCheckJob.KindName, new ScheduleCheckJob.Payload(schedule.Id), maxAttempts: 1, ownerId: schedule.OwnerId, cancellationToken: cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}

/// <summary>
/// Checks one schedule as its owner: a time schedule simply runs; a feed or inbox runs only when something new arrived.
/// The first check of a feed or inbox only remembers what is there.
/// </summary>
public sealed partial class ScheduleCheckJob(
    IScheduleRepository schedules,
    IFeedReader feeds,
    IIntegrationRepository integrations,
    IntegrationService integrationService,
    PluginRegistry<IIntegrationKind> kinds,
    ScheduleRunner runner,
    CurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IJobHandler
{
    public const string KindName = "schedule.check";

    public string Kind => KindName;

    public sealed record Payload(Guid ScheduleId);

    public async Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var schedule = await schedules.FindAsync(JobScheduler.ReadPayload<Payload>(job).ScheduleId, cancellationToken);
        if (schedule is not { IsEnabled: true })
        {
            return "The schedule is gone or switched off.";
        }

        if (schedule.Trigger == TriggerKind.Time)
        {
            return await runner.RunAsync(schedule, null, cancellationToken);
        }

        var (ids, describe) = schedule.Trigger switch
        {
            TriggerKind.Rss => await FeedAsync(schedule, cancellationToken),
            TriggerKind.Email => await InboxAsync(schedule, cancellationToken),
            _ => ((IReadOnlyList<string>)[], (Func<IEnumerable<string>, string>)(_ => string.Empty)),
        };

        var seen = schedule.LastSeen?.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var fresh = seen is null ? [] : ids.Where(id => !seen.Contains(id)).ToList();
        schedule.Checked(schedule.NextCheckAt, string.Join('\n', ids.Take(100)), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (seen is null)
        {
            return $"Started watching: {ids.Count} items are already there.";
        }

        return fresh.Count == 0 ? "Nothing new." : await runner.RunAsync(schedule, describe(fresh), cancellationToken);
    }

    private async Task<(IReadOnlyList<string>, Func<IEnumerable<string>, string>)> FeedAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        var items = await feeds.ReadAsync(schedule.FeedUrl!, restrictNetwork: !currentUser.IsAdmin, cancellationToken);
        return ([.. items.Select(i => i.Id)], fresh =>
        {
            var wanted = fresh.ToHashSet(StringComparer.Ordinal);
            var text = new StringBuilder("New in the feed:\n");
            foreach (var item in items.Where(i => wanted.Contains(i.Id)).Take(20))
            {
                text.Append("- ").Append(item.Title);
                if (item.Link is not null)
                {
                    text.Append(" (").Append(item.Link).Append(')');
                }

                if (!string.IsNullOrWhiteSpace(item.Summary))
                {
                    text.Append(": ").Append(item.Summary.Length <= 300 ? item.Summary : item.Summary[..300]);
                }

                text.Append('\n');
            }

            return text.ToString();
        }
        );
    }

    private async Task<(IReadOnlyList<string>, Func<IEnumerable<string>, string>)> InboxAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        var integration = await integrations.FindAsync(schedule.IntegrationId!.Value, cancellationToken)
            ?? throw new InvalidOperationException("The email account of this schedule is gone.");
        var list = kinds.Get(integration.Kind).CreateTools(integrationService.ConfigFor(integration)).Single(t => t.Kind == "email_list_recent");
        var text = await list.InvokeAsync(new ToolInvocation(schedule.OrbId, currentUser.UserId, """{"count":25,"unreadOnly":true}"""), cancellationToken);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var byId = lines.Select(l => (Line: l, Match: MessageId().Match(l))).Where(x => x.Match.Success)
            .ToDictionary(x => x.Match.Groups[1].Value, x => x.Line.Trim(), StringComparer.Ordinal);
        return ([.. byId.Keys], fresh => "New unread email:\n" + string.Join('\n', fresh.Select(id => "- " + byId[id])));
    }

    [GeneratedRegex(@"^id (\d+)")]
    private static partial Regex MessageId();
}

/// <summary>Runs a schedule now, for "run now" and webhooks. Input is what the webhook sent.</summary>
public sealed class ScheduleRunJob(IScheduleRepository schedules, ScheduleRunner runner) : IJobHandler
{
    public const string KindName = "schedule.run";

    public string Kind => KindName;

    public sealed record Payload(Guid ScheduleId, string? Input);

    public async Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobScheduler.ReadPayload<Payload>(job);
        var schedule = await schedules.FindAsync(payload.ScheduleId, cancellationToken)
            ?? throw new InvalidOperationException("The schedule is gone.");
        return await runner.RunAsync(schedule, payload.Input, cancellationToken);
    }
}

/// <summary>Has the orb answer a schedule's prompt in the schedule's own chat, as if the person had asked.</summary>
public sealed class ScheduleRunner(ChatService chat, IOrbRepository orbs, Notifications.NotificationService notifications, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<string> RunAsync(Schedule schedule, string? input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var conversationId = schedule.ConversationId;
        if (conversationId is null || !await ExistsAsync(conversationId.Value, cancellationToken))
        {
            conversationId = (await chat.StartAsync(new StartConversationRequest(schedule.OrbId), cancellationToken)).Id;
        }

        schedule.Ran(conversationId.Value, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var message = new StringBuilder().Append("[Scheduled: ").Append(schedule.Name).Append("] ").Append(schedule.Prompt);
        if (!string.IsNullOrWhiteSpace(input))
        {
            message.Append("\n\n").Append(input);
        }

        var reply = new StringBuilder();
        string? outcome = null;
        await foreach (var e in await chat.SendAsync(conversationId.Value, new SendMessageRequest(message.ToString()), cancellationToken))
        {
            switch (e.Type)
            {
                case ChatEvent.DeltaType:
                    reply.Append(e.Text);
                    break;
                case ChatEvent.ApprovalType:
                    outcome = $"Waiting for your approval to use {e.Tool}.";
                    break;
                case ChatEvent.ErrorType:
                    throw new InvalidOperationException(e.Text);
            }
        }

        var text = reply.ToString();
        var summary = outcome ?? (text.Length <= 200 ? text : string.Concat(text.AsSpan(0, 197), "..."));

        // Proactive: the orb reaches out with what it wrote, or with what it needs.
        var orb = await orbs.FindAsync(schedule.OrbId, cancellationToken);
        await notifications.NotifyAsync(
            schedule.OwnerId,
            new Notifications.Notification($"{orb?.Name ?? "Your orb"}: {schedule.Name}", summary, $"/orbs/{schedule.OrbId}/chat/{conversationId}"),
            cancellationToken);
        return summary;
    }

    private async Task<bool> ExistsAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        try
        {
            _ = await chat.GetAsync(conversationId, 1, cancellationToken);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }
}

internal static class ScheduleMapping
{
    public static ScheduleDto ToDto(this Schedule s) => new(
        s.Id, s.OrbId, s.Name, s.Prompt, s.Trigger, s.Cron, s.TimeZone, s.FeedUrl, s.IntegrationId, s.IsEnabled, s.NextCheckAt, s.LastRunAt, s.ConversationId, s.CreatedAt);
}
