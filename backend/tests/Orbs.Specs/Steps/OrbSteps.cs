using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Orbs;
using Orbs.Domain.Orbs;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class OrbSteps(SpecContext context)
{
    [Given("an orb named {string}")]
    public async Task GivenAnOrbNamed(string name) =>
        context.Orbs[name] = await context.Api.CreateOrbAsync(new SaveOrbRequest(name));

    [When("I create an orb named {string} described as {string}")]
    public async Task WhenICreateAnOrbDescribedAs(string name, string description) =>
        context.Orbs[name] = await context.Api.CreateOrbAsync(new SaveOrbRequest(name, description));

    [When("I try to create an orb named {string}")]
    public async Task WhenITryToCreateAnOrbNamed(string name) =>
        context.LastResponse = await context.Api.Http.PostAsJsonAsync("/api/orbs", new SaveOrbRequest(name), ApiClient.Json);

    [When("I give {string} the color {string} and the eyes {string}")]
    public async Task WhenIGiveTheColorAndEyes(string name, string color, string eyes)
    {
        var orb = context.Orbs[name];
        var look = orb.Appearance with
        {
            Color = Enum.Parse<OrbColor>(color, ignoreCase: true),
            Eyes = Enum.Parse<OrbEyes>(eyes, ignoreCase: true),
        };
        var response = await context.Api.Http.PutAsJsonAsync($"/api/orbs/{orb.Id}", new SaveOrbRequest(orb.Name, orb.Description, Appearance: look), ApiClient.Json);
        context.Orbs[name] = await ApiClient.ReadAsync<OrbDto>(response);
    }

    [When("I delete the orb {string}")]
    public async Task WhenIDeleteTheOrb(string name) =>
        (await context.Api.Http.DeleteAsync(new Uri($"/api/orbs/{context.Orbs[name].Id}", UriKind.Relative))).EnsureSuccessStatusCode();

    [Then("I have {int} orb(s)")]
    public async Task ThenIHaveOrbs(int count) =>
        (await context.Api.GetAsync<List<OrbDto>>("/api/orbs")).Count.ShouldBe(count);

    [Then("the orb {string} is described as {string}")]
    public async Task ThenTheOrbIsDescribedAs(string name, string description) =>
        (await Reload(name)).Description.ShouldBe(description);

    [Then("the orb {string} uses the default look")]
    public async Task ThenTheOrbUsesTheDefaultLook(string name) =>
        (await Reload(name)).Appearance.ShouldBe(OrbAppearance.Default);

    [Then("{string} has the color {string} and the eyes {string}")]
    public async Task ThenHasTheColorAndEyes(string name, string color, string eyes)
    {
        var orb = await Reload(name);
        orb.Appearance.Color.ToString().ShouldBe(color, StringCompareShould.IgnoreCase);
        orb.Appearance.Eyes.ToString().ShouldBe(eyes, StringCompareShould.IgnoreCase);
    }

    [Then("the request is refused because {string}")]
    public async Task ThenTheRequestIsRefusedBecause(string reason)
    {
        context.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await context.LastResponse.Content.ReadAsStringAsync()).ShouldContain(reason);
    }

    private Task<OrbDto> Reload(string name) => context.Api.GetAsync<OrbDto>($"/api/orbs/{context.Orbs[name].Id}");
}
