using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Jobs;
using Orbs.Domain.Jobs;

namespace Orbs.Testing;

/// <summary>Answers with its payload's text and the number of orbs it can see, as the job's owner.</summary>
public sealed class EchoJobHandler(IOrbRepository orbs, CurrentUser currentUser) : IJobHandler
{
    public const string KindName = "test.echo";

    public string Kind => KindName;

    public async Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobScheduler.ReadPayload<EchoPayload>(job);
        var visible = (await orbs.ListAsync(cancellationToken)).Count;
        return $"{payload.Text} (user {currentUser.UserId}, {visible} orbs)";
    }

    public sealed record EchoPayload(string Text);
}

/// <summary>Always fails, for testing retries.</summary>
public sealed class FailingJobHandler : IJobHandler
{
    public const string KindName = "test.fail";

    public string Kind => KindName;

    public Task<string?> HandleAsync(Job job, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("job exploded");
}
