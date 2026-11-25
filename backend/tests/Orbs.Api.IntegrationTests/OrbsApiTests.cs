using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Orbs;
using Orbs.Domain.Orbs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class OrbsApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public OrbsApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_orb_can_be_created_read_updated_and_deleted()
    {
        var created = await _api.CreateOrbAsync(new SaveOrbRequest("Pip", "Work sidekick"));

        var read = await _api.GetAsync<OrbDto>($"/api/orbs/{created.Id}");
        read.Name.ShouldBe("Pip");

        var look = OrbAppearance.Default with { Color = OrbColor.Coral, Accessory = OrbAccessory.Crown };
        var updated = await ApiClient.ReadAsync<OrbDto>(
            await _api.Http.PutAsJsonAsync($"/api/orbs/{created.Id}", new SaveOrbRequest("Ember", Appearance: look), ApiClient.Json));
        updated.Name.ShouldBe("Ember");
        updated.Appearance.Color.ShouldBe(OrbColor.Coral);

        (await _api.Http.DeleteAsync(new Uri($"/api/orbs/{created.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.Http.GetAsync(new Uri($"/api/orbs/{created.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_returns_201_with_a_location()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/orbs", new SaveOrbRequest("Pip"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldStartWith("/api/orbs/");
    }

    [Fact]
    public async Task Broken_rules_come_back_as_problem_details()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/orbs", new SaveOrbRequest(""), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync()).ShouldContain("Name is required.");
    }

    [Fact]
    public async Task Appearance_is_serialized_with_readable_names()
    {
        var created = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));

        var json = await _api.Http.GetStringAsync(new Uri($"/api/orbs/{created.Id}", UriKind.Relative));

        json.ShouldContain("\"color\":\"plasma\"");
        json.ShouldContain("\"accessory\":\"antenna\"");
    }
}
