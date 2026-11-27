using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Accounts;
using Orbs.Application.Orbs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class AccountApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_fresh_server_asks_for_the_first_account_and_refuses_everything_else()
    {
        var anonymous = _factory.CreateAnonymousClient();

        var status = await ApiClient.ReadAsync<AuthStatusDto>(await anonymous.GetAsync(new Uri("/api/auth/status", UriKind.Relative)));
        status.SetupRequired.ShouldBeTrue();
        status.Oidc.ShouldBeNull();

        (await anonymous.GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_only_works_once()
    {
        _ = _factory.CreateClient();
        var again = await _factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/setup", new SetupRequest("x@orbs.test", null, OrbsApiFactory.Password), ApiClient.Json);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_wrong_password_is_refused_without_saying_which_part_was_wrong()
    {
        _ = _factory.CreateClient();
        var response = await _factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(OrbsApiFactory.OwnerEmail, "wrong password!"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).ShouldContain("do not match");
    }

    [Fact]
    public async Task Signing_in_sets_an_http_only_cookie_that_works_on_its_own()
    {
        _ = _factory.CreateClient();
        var browser = _factory.CreateAnonymousClient();

        var response = await browser.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(OrbsApiFactory.OwnerEmail, OrbsApiFactory.Password), ApiClient.Json);

        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.Contains("orbs_session=") && c.Contains("httponly"));
        (await browser.GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Each_user_sees_only_their_own_orbs()
    {
        var owner = new ApiClient(_factory.CreateClient());
        var sam = new ApiClient(await _factory.CreateClientForNewUserAsync("sam@orbs.test"));
        var ownersOrb = await owner.CreateOrbAsync(new SaveOrbRequest("Pip"));
        await sam.CreateOrbAsync(new SaveOrbRequest("Basil"));

        (await owner.GetAsync<List<OrbDto>>("/api/orbs")).Select(o => o.Name).ShouldBe(["Pip"]);
        (await sam.GetAsync<List<OrbDto>>("/api/orbs")).Select(o => o.Name).ShouldBe(["Basil"]);
        (await sam.Http.GetAsync(new Uri($"/api/orbs/{ownersOrb.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_admins_manage_accounts()
    {
        var owner = new ApiClient(_factory.CreateClient());
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");

        (await sam.GetAsync(new Uri("/api/users", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await owner.GetAsync<List<UserDto>>("/api/users")).Select(u => u.Email).ShouldBe([OrbsApiFactory.OwnerEmail, "sam@orbs.test"]);
    }

    [Fact]
    public async Task Deleting_a_user_deletes_what_they_own()
    {
        var owner = new ApiClient(_factory.CreateClient());
        var sam = new ApiClient(await _factory.CreateClientForNewUserAsync("sam@orbs.test"));
        await sam.CreateOrbAsync(new SaveOrbRequest("Basil"));
        var samId = (await owner.GetAsync<List<UserDto>>("/api/users")).Single(u => u.Email == "sam@orbs.test").Id;

        (await owner.Http.DeleteAsync(new Uri($"/api/users/{samId}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await sam.Http.GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        var client = _factory.CreateClient();

        (await client.PostAsync(new Uri("/api/auth/sign-out", UriKind.Relative), null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_account_lists_this_session_as_current_and_passwords_can_change()
    {
        var api = new ApiClient(_factory.CreateClient());

        var account = await api.GetAsync<AccountDto>("/api/account");
        account.User.IsAdmin.ShouldBeTrue();
        account.Sessions.ShouldContain(s => s.Current);

        var change = await api.Http.PostAsJsonAsync("/api/account/password", new ChangePasswordRequest("not it at all", "a brand new password"), ApiClient.Json);
        change.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        change = await api.Http.PostAsJsonAsync("/api/account/password", new ChangePasswordRequest(OrbsApiFactory.Password, "a brand new password"), ApiClient.Json);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Passkey_sign_in_starts_with_webauthn_options()
    {
        var options = await ApiClient.ReadAsync<PasskeyOptionsDto>(
            await _factory.CreateAnonymousClient().PostAsync(new Uri("/api/auth/passkeys/sign-in/options", UriKind.Relative), null));

        options.CeremonyId.ShouldNotBeNullOrEmpty();
        options.Options.ShouldContain("challenge");
    }
}
