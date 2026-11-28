using System.Net;
using Orbs.Application.Accounts;
using Orbs.Application.Orbs;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class AccountSteps(SpecContext context)
{
    [Given("a server with the demo data and no accounts")]
    public void GivenAServerWithTheDemoData() => context.UseServer(new OrbsApiFactory { SeedDemoData = true });

    [When("I create the first account")]
    public void WhenICreateTheFirstAccount() => context.SignInAsOwner();

    [Then("I am an admin")]
    public async Task ThenIAmAnAdmin() =>
        (await context.Api.GetAsync<AccountDto>("/api/account")).User.IsAdmin.ShouldBeTrue();

    [Given("another account {string}")]
    public async Task GivenAnotherAccount(string email) =>
        context.Users[email] = new ApiClient(await context.Factory.CreateClientForNewUserAsync(email));

    [Then("{string} has no orbs")]
    public async Task ThenHasNoOrbs(string email) =>
        (await context.Users[email].GetAsync<List<OrbDto>>("/api/orbs")).ShouldBeEmpty();

    [Then("a visitor who is not signed in is refused")]
    public async Task ThenAVisitorIsRefused() =>
        (await context.Factory.CreateAnonymousClient().GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
