using System.Net.Http.Json;
using Orbs.Application.Orbs;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class OrbTemplateSteps(SpecContext context)
{
    private OrbTemplate? _template;
    private ApiClient? _someoneElse;

    [When("I export {string} as a template")]
    public async Task WhenIExport(string orb) =>
        _template = await context.Api.GetAsync<OrbTemplate>($"/api/orbs/{context.Orbs[orb].Id}/template");

    [When("someone else imports that template")]
    public async Task WhenSomeoneElseImports()
    {
        _someoneElse = new ApiClient(await context.Factory.CreateClientForNewUserAsync("friend@orbs.test"));
        (await _someoneElse.Http.PostAsJsonAsync("/api/orbs/import", _template, ApiClient.Json)).EnsureSuccessStatusCode();
    }

    [Then("they have an orb named {string}")]
    public async Task ThenTheyHave(string name) =>
        (await _someoneElse!.GetAsync<List<OrbDto>>("/api/orbs")).Select(o => o.Name).ShouldBe([name]);
}
