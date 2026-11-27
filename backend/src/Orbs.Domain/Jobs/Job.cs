using Orbs.Domain.Common;

namespace Orbs.Domain.Jobs;

public enum JobStatus { Pending, Running, Succeeded, Failed }

/// <summary>
/// A piece of work that runs in the background, such as a scheduled orb run or a context sync.
/// <see cref="Kind"/> names the handler; <see cref="Payload"/> is its input as JSON.
/// Failed attempts are retried with a growing delay until <see cref="MaxAttempts"/>.
/// </summary>
public sealed class Job : OwnedAggregateRoot
{
    public const int MaxKindLength = 64;
    public const int MaxPayloadLength = 100_000;
    public const int MaxErrorLength = 2_000;
    public const int MaxResultLength = 2_000;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(30);

    private Job(Guid id, string kind, string payload, DateTimeOffset runAt, int maxAttempts, DateTimeOffset now) : base(id, now)
    {
        Kind = kind;
        Payload = payload;
        RunAt = runAt;
        MaxAttempts = maxAttempts;
    }

    // For EF Core materialization.
    private Job()
    {
        Kind = string.Empty;
        Payload = string.Empty;
    }

    public string Kind { get; private set; }

    public string Payload { get; private set; }

    public JobStatus Status { get; private set; } = JobStatus.Pending;

    /// <summary>The earliest moment the job may start.</summary>
    public DateTimeOffset RunAt { get; private set; }

    public int Attempts { get; private set; }

    public int MaxAttempts { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>A short, human-readable outcome, shown in the app.</summary>
    public string? Result { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public static Job Create(string kind, string? payload, DateTimeOffset runAt, DateTimeOffset now, int maxAttempts = 3)
    {
        if (maxAttempts < 1)
        {
            throw new DomainException("A job needs at least one attempt.");
        }

        return new Job(
            Guid.NewGuid(),
            Guard.Text(kind, nameof(Kind), MaxKindLength),
            Guard.Text(payload ?? "{}", nameof(Payload), MaxPayloadLength),
            runAt,
            maxAttempts,
            now);
    }

    public bool IsDue(DateTimeOffset now) => Status == JobStatus.Pending && RunAt <= now;

    public void Start(DateTimeOffset now)
    {
        if (Status != JobStatus.Pending)
        {
            throw new DomainException($"Only pending jobs can start; this one is {Status}.");
        }

        Status = JobStatus.Running;
        Attempts++;
        StartedAt = now;
        Touch(now);
    }

    public void Succeed(string? result, DateTimeOffset now)
    {
        Status = JobStatus.Succeeded;
        Result = Clip(result, MaxResultLength);
        LastError = null;
        FinishedAt = now;
        Touch(now);
    }

    /// <summary>Records a failure. Retries later (30 s, 60 s, 120 s, ...) while attempts remain.</summary>
    public void Fail(string error, DateTimeOffset now)
    {
        LastError = Clip(error, MaxErrorLength);
        if (Attempts < MaxAttempts)
        {
            Status = JobStatus.Pending;
            RunAt = now + (BaseRetryDelay * Math.Pow(2, Attempts - 1));
        }
        else
        {
            Status = JobStatus.Failed;
            FinishedAt = now;
        }

        Touch(now);
    }

    /// <summary>A job that was running when the server stopped goes back in the queue.</summary>
    public void Requeue(DateTimeOffset now)
    {
        if (Status == JobStatus.Running)
        {
            Status = JobStatus.Pending;
            RunAt = now;
            Touch(now);
        }
    }

    private static string? Clip(string? text, int max) =>
        text is null ? null : text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 3), "...");
}
