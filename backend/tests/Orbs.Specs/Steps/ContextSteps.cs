using System.Net.Http.Json;
using Orbs.Application.Context;
using Orbs.Application.Orbs;
using Orbs.Domain.Context;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class ContextSteps(SpecContext context)
{
    [Given("a note {string} saying {string}")]
    [When("I add a note {string} saying {string}")]
    public async Task AddNote(string name, string text) =>
        context.Sources[name] = await context.Api.AddNoteAsync(name, text);

    [Given("an orb named {string} that knows {string}")]
    [When("I create an orb named {string} that knows {string}")]
    public async Task CreateOrbThatKnows(string orbName, string sourceName) =>
        context.Orbs[orbName] = await context.Api.CreateOrbAsync(new SaveOrbRequest(orbName, ContextSourceIds: [context.Sources[sourceName].Id]));

    [When("I add the web page {string} as {string}")]
    public async Task WhenIAddTheWebPage(string url, string name)
    {
        var response = await context.Api.Http.PostAsJsonAsync("/api/context-sources", new SaveContextSourceRequest(name, "url", Location: url), ApiClient.Json);
        context.Sources[name] = await ApiClient.ReadAsync<ContextSourceDto>(response);
    }

    [When("I add the web page {string} as {string} and keep it in sync every day")]
    public async Task WhenIAddAPageKeptInSync(string url, string name)
    {
        var response = await context.Api.Http.PostAsJsonAsync("/api/context-sources", new SaveContextSourceRequest(name, "url", Location: url, SyncEvery: SyncInterval.Daily), ApiClient.Json);
        context.Sources[name] = await ApiClient.ReadAsync<ContextSourceDto>(response);
    }

    [Then("the source {string} syncs again within a day")]
    public async Task ThenSyncsWithinADay(string name)
    {
        var source = await context.Api.GetAsync<ContextSourceDto>($"/api/context-sources/{context.Sources[name].Id}");
        source.SyncEvery.ShouldBe(SyncInterval.Daily);
        source.NextSyncAt.ShouldNotBeNull().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddDays(1).AddMinutes(1));
    }

    [Then("the source {string} failed because {string}")]
    public async Task ThenTheSourceFailedBecause(string name, string reason)
    {
        var source = await context.Api.GetAsync<ContextSourceDto>($"/api/context-sources/{context.Sources[name].Id}");
        source.Status.ShouldBe(ContextSourceStatus.Failed);
        source.LastError.ShouldNotBeNull().ShouldContain(reason);
    }

    [When("I delete the source {string}")]
    public async Task WhenIDeleteTheSource(string name) =>
        (await context.Api.Http.DeleteAsync(new Uri($"/api/context-sources/{context.Sources[name].Id}", UriKind.Relative))).EnsureSuccessStatusCode();

    [Then("the source {string} is ready")]
    public async Task ThenTheSourceIsReady(string name) =>
        (await context.Api.GetAsync<ContextSourceDto>($"/api/context-sources/{context.Sources[name].Id}")).Status.ShouldBe(ContextSourceStatus.Ready);

    [Then("{string} knows {string}")]
    public async Task ThenKnows(string orbName, string sourceName) =>
        (await Reload(orbName)).ContextSourceIds.ShouldContain(context.Sources[sourceName].Id);

    [Then("{string} does not know {string}")]
    public async Task ThenDoesNotKnow(string orbName, string sourceName) =>
        (await Reload(orbName)).ContextSourceIds.ShouldNotContain(context.Sources[sourceName].Id);

    [Then("{string} knows nothing")]
    public async Task ThenKnowsNothing(string orbName) =>
        (await Reload(orbName)).ContextSourceIds.ShouldBeEmpty();

    private Task<OrbDto> Reload(string name) => context.Api.GetAsync<OrbDto>($"/api/orbs/{context.Orbs[name].Id}");
}
