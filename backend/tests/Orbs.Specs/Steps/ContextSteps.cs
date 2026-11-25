using Orbs.Application.Context;
using Orbs.Application.Orbs;
using Orbs.Domain.Context;
using Orbs.Specs.Support;
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
