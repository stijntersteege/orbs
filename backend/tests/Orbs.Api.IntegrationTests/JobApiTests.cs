using Orbs.Application.Accounts;
using Orbs.Application.Jobs;
using Orbs.Application.Orbs;
using Orbs.Domain.Jobs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class JobApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_job_runs_as_its_owner_and_reports_its_outcome()
    {
        var owner = new ApiClient(_factory.CreateClient());
        var sam = new ApiClient(await _factory.CreateClientForNewUserAsync("sam@orbs.test"));
        await owner.CreateOrbAsync(new SaveOrbRequest("Pip"));
        await owner.CreateOrbAsync(new SaveOrbRequest("Nova"));
        var samId = (await sam.GetAsync<AccountDto>("/api/account")).User.Id;

        await _factory.EnqueueAsync(EchoJobHandler.KindName, new EchoJobHandler.EchoPayload("hello"), samId);
        (await _factory.RunJobsAsync()).ShouldBe(1);

        var job = (await sam.GetAsync<List<JobDto>>("/api/jobs")).ShouldHaveSingleItem();
        job.Status.ShouldBe(JobStatus.Succeeded);
        job.Result.ShouldBe($"hello (user {samId}, 0 orbs)");
        (await owner.GetAsync<List<JobDto>>("/api/jobs")).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failing_job_is_retried_later_and_keeps_the_error()
    {
        var owner = new ApiClient(_factory.CreateClient());
        var ownerId = (await owner.GetAsync<AccountDto>("/api/account")).User.Id;

        await _factory.EnqueueAsync(FailingJobHandler.KindName, new { }, ownerId);
        await _factory.RunJobsAsync();
        (await _factory.RunJobsAsync()).ShouldBe(0, "the retry waits 30 seconds");

        var job = (await owner.GetAsync<List<JobDto>>("/api/jobs")).ShouldHaveSingleItem();
        job.Status.ShouldBe(JobStatus.Pending);
        job.Attempts.ShouldBe(1);
        job.LastError.ShouldBe("job exploded");
    }

    [Fact]
    public async Task Unknown_kinds_fail_with_a_clear_message()
    {
        var owner = new ApiClient(_factory.CreateClient());
        var ownerId = (await owner.GetAsync<AccountDto>("/api/account")).User.Id;

        await _factory.EnqueueAsync("nobody.handles.this", new { }, ownerId);
        await _factory.RunJobsAsync();

        (await owner.GetAsync<List<JobDto>>("/api/jobs")).Single().LastError.ShouldNotBeNull().ShouldContain("No handler");
    }
}
