using Orbs.Application.Accounts;
using Orbs.Application.Jobs;
using Orbs.Domain.Jobs;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class JobSteps(SpecContext context)
{
    [Given("a background job for me that says {string}")]
    public async Task GivenAJobForMe(string text)
    {
        var me = (await context.Api.GetAsync<AccountDto>("/api/account")).User.Id;
        await context.Factory.EnqueueAsync(EchoJobHandler.KindName, new EchoJobHandler.EchoPayload(text), me);
    }

    [Given("a background job for {string} that says {string}")]
    public async Task GivenAJobFor(string email, string text)
    {
        var id = (await context.Users[email].GetAsync<AccountDto>("/api/account")).User.Id;
        await context.Factory.EnqueueAsync(EchoJobHandler.KindName, new EchoJobHandler.EchoPayload(text), id);
    }

    [When("the background jobs run")]
    public Task WhenTheJobsRun() => context.Factory.RunJobsAsync();

    [Then("my latest background job succeeded with {string}")]
    public async Task ThenMyLatestJobSucceeded(string text)
    {
        var job = (await context.Api.GetAsync<List<JobDto>>("/api/jobs"))[0];
        job.Status.ShouldBe(JobStatus.Succeeded);
        job.Result.ShouldNotBeNull().ShouldStartWith(text);
    }

    [Then("I have no background jobs")]
    public async Task ThenIHaveNoJobs() =>
        (await context.Api.GetAsync<List<JobDto>>("/api/jobs")).ShouldBeEmpty();
}
