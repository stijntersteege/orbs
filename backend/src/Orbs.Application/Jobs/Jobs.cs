using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Domain.Jobs;

namespace Orbs.Application.Jobs;

public interface IJobRepository : IRepository<Job>
{
    /// <summary>Pending jobs whose time has come, oldest first. Called as the system, across all users.</summary>
    Task<IReadOnlyList<Job>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> ListRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>The current user's most recent jobs.</summary>
    Task<IReadOnlyList<Job>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task<bool> AnyPendingAsync(string kind, CancellationToken cancellationToken = default);
}

/// <summary>Does one kind of background work. Add a class and register it to support a new kind.</summary>
public interface IJobHandler
{
    string Kind { get; }

    /// <summary>Runs the job inside a scope signed in as its owner. Returns a short outcome for the app.</summary>
    Task<string?> HandleAsync(Job job, CancellationToken cancellationToken);
}

public sealed record JobDto(
    Guid Id,
    string Kind,
    JobStatus Status,
    DateTimeOffset RunAt,
    int Attempts,
    int MaxAttempts,
    string? Result,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

/// <summary>Puts work in the queue. Jobs belong to the current user, or to nobody when enqueued as the system.</summary>
public sealed class JobScheduler(IJobRepository jobs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Job> EnqueueAsync(string kind, object? payload = null, DateTimeOffset? runAt = null, int maxAttempts = 3, Guid? ownerId = null, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var job = Job.Create(kind, payload is null ? "{}" : JsonSerializer.Serialize(payload, Json), runAt ?? now, now, maxAttempts);
        if (ownerId is { } owner && owner != Guid.Empty)
        {
            job.AssignOwner(owner);
        }

        jobs.Add(job);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<IReadOnlyList<JobDto>> ListRecentAsync(CancellationToken cancellationToken = default) =>
        [.. (await jobs.ListRecentAsync(50, cancellationToken)).Select(ToDto)];

    public static T ReadPayload<T>(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return JsonSerializer.Deserialize<T>(job.Payload, Json) ?? throw new InvalidOperationException($"Job {job.Id} has an empty payload.");
    }

    private static JobDto ToDto(Job j) =>
        new(j.Id, j.Kind, j.Status, j.RunAt, j.Attempts, j.MaxAttempts, j.Result, j.LastError, j.CreatedAt, j.FinishedAt);
}

/// <summary>
/// Runs due jobs. Each job runs in its own scope, signed in as the job's owner, so handlers see
/// exactly what that user sees. A failure in one job never stops the others.
/// </summary>
public sealed class JobRunner(IServiceScopeFactory scopes, TimeProvider clock)
{
    public const int BatchSize = 20;

    /// <summary>Puts jobs that were running when the server stopped back in the queue.</summary>
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = SystemScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var now = clock.GetUtcNow();
        foreach (var job in await jobs.ListRunningAsync(cancellationToken))
        {
            job.Requeue(now);
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    /// <summary>Runs every job that is due now. Returns how many ran.</summary>
    public async Task<int> RunDueAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> due;
        await using (var scope = SystemScope())
        {
            due = [.. (await scope.ServiceProvider.GetRequiredService<IJobRepository>().ListDueAsync(clock.GetUtcNow(), BatchSize, cancellationToken)).Select(j => j.Id)];
        }

        foreach (var id in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RunOneAsync(id, cancellationToken);
        }

        return due.Count;
    }

    private async Task RunOneAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var scope = SystemScope();
        var services = scope.ServiceProvider;
        var jobs = services.GetRequiredService<IJobRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var job = await jobs.FindAsync(jobId, cancellationToken);
        if (job is null || !job.IsDue(clock.GetUtcNow()))
        {
            return;
        }

        job.Start(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var handler = services.GetServices<IJobHandler>().FirstOrDefault(h => string.Equals(h.Kind, job.Kind, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No handler for jobs of kind '{job.Kind}'.");

            // From here on the scope acts as the job's owner: their data, their filters.
            var currentUser = services.GetRequiredService<CurrentUser>();
            if (job.OwnerId != Guid.Empty)
            {
                var owner = await services.GetRequiredService<IUserRepository>().FindAsync(job.OwnerId, cancellationToken);
                currentUser.SignIn(job.OwnerId, owner?.IsAdmin ?? false);
            }

            var result = await handler.HandleAsync(job, cancellationToken);
            job.Succeed(result, clock.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            job.Fail(ex.Message, clock.GetUtcNow());
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private AsyncServiceScope SystemScope()
    {
        var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().ActAsSystem();
        return scope;
    }
}

/// <summary>Daily clean-up of expired sessions. Runs as the system.</summary>
public sealed class SessionCleanupJob(ISessionRepository sessions, TimeProvider clock) : IJobHandler
{
    public const string KindName = "sessions.cleanup";

    public string Kind => KindName;

    public async Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var removed = await sessions.DeleteExpiredAsync(clock.GetUtcNow(), cancellationToken);
        return removed == 1 ? "Removed 1 expired session." : $"Removed {removed} expired sessions.";
    }
}

/// <summary>Keeps the recurring system jobs in the queue.</summary>
public sealed class Housekeeping(IJobRepository jobs, JobScheduler queue, TimeProvider clock)
{
    public async Task EnsureScheduledAsync(CancellationToken cancellationToken = default)
    {
        if (!await jobs.AnyPendingAsync(SessionCleanupJob.KindName, cancellationToken))
        {
            await queue.EnqueueAsync(SessionCleanupJob.KindName, runAt: clock.GetUtcNow().AddDays(1), maxAttempts: 1, cancellationToken: cancellationToken);
        }
    }
}
