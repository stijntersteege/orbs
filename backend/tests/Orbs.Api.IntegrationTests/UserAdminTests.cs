using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Accounts;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class UserAdminTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public UserAdminTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_renames_someone_and_makes_them_admin()
    {
        var sam = await CreateAsync("sam@orbs.test");

        var updated = await ApiClient.ReadAsync<UserDto>(await _api.Http.PutAsJsonAsync($"/api/users/{sam.Id}", new UpdateUserRequest("Sam S.", true), ApiClient.Json));

        updated.DisplayName.ShouldBe("Sam S.");
        updated.IsAdmin.ShouldBeTrue();
    }

    [Fact]
    public async Task A_new_password_signs_the_person_out_everywhere()
    {
        var samClient = await _factory.CreateClientForNewUserAsync("sam@orbs.test");
        var sam = (await _api.GetAsync<List<UserDto>>("/api/users")).Single(u => u.Email == "sam@orbs.test");

        (await _api.Http.PutAsJsonAsync($"/api/users/{sam.Id}", new UpdateUserRequest("Sam", false, "a-brand-new-password"), ApiClient.Json)).EnsureSuccessStatusCode();

        (await samClient.GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var signIn = await _factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("sam@orbs.test", "a-brand-new-password"), ApiClient.Json);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admins_cannot_drop_their_own_admin_rights()
    {
        var me = (await _api.GetAsync<List<UserDto>>("/api/users")).Single(u => u.IsAdmin);

        var response = await _api.Http.PutAsJsonAsync($"/api/users/{me.Id}", new UpdateUserRequest("Owner", false), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task People_who_are_not_admins_cannot_change_accounts()
    {
        var samClient = await _factory.CreateClientForNewUserAsync("sam@orbs.test");
        var me = (await _api.GetAsync<List<UserDto>>("/api/users")).Single(u => u.IsAdmin);

        var response = await samClient.PutAsJsonAsync($"/api/users/{me.Id}", new UpdateUserRequest("Hacked", false), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<UserDto> CreateAsync(string email) =>
        await ApiClient.ReadAsync<UserDto>(await _api.Http.PostAsJsonAsync("/api/users", new CreateUserRequest(email, null, "a-long-password-1"), ApiClient.Json));
}
