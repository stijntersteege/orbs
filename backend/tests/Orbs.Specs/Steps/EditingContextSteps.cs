using System.Net.Http.Json;
using Orbs.Application.Context;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class EditingContextSteps(SpecContext context)
{
    [When("I change {string} to {string}")]
    public async Task WhenIChange(string source, string text) =>
        (await context.Api.Http.PutAsJsonAsync($"/api/context-sources/{context.Sources[source].Id}/file", new ReplaceFileTextRequest(text), ApiClient.Json)).EnsureSuccessStatusCode();

    [Then("{string} holds {string}")]
    public async Task ThenHolds(string source, string text) =>
        (await context.Api.GetAsync<ContextContentDto>($"/api/context-sources/{context.Sources[source].Id}/content")).Content.ShouldBe(text);
}
