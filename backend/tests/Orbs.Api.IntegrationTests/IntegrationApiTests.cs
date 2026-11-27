using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Tools;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class IntegrationApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public IntegrationApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task The_four_starter_apps_are_offered()
    {
        var kinds = await _api.GetAsync<List<IntegrationKindDto>>("/api/integrations/kinds");

        kinds.Select(k => k.Kind).ShouldBe(["caldav", "email", "home-assistant", "web-search"], ignoreOrder: true);
        kinds.Single(k => k.Kind == "home-assistant").Fields.Single(f => f.Secret).Key.ShouldBe("token");
    }

    [Fact]
    public async Task Connecting_an_app_adds_its_tools_and_keeps_the_secret_secret()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/integrations", new SaveIntegrationRequest(
            "home-assistant", "Home", new Dictionary<string, string> { ["url"] = "http://ha.local:8123" }, "super-secret-token"), ApiClient.Json);
        var integration = await ApiClient.ReadAsync<IntegrationDto>(response);

        integration.HasSecret.ShouldBeTrue();
        integration.Tools.ShouldBe(["home_list_entities", "home_call_service"]);
        (await _api.Http.GetStringAsync(new Uri("/api/integrations", UriKind.Relative))).ShouldNotContain("super-secret-token");
        (await _api.GetAsync<List<ToolDto>>("/api/tools")).Select(t => t.Name).ShouldContain("home_call_service");
    }

    [Fact]
    public async Task Required_settings_are_checked()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/integrations", new SaveIntegrationRequest("caldav", "Calendar"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Calendar address is required");
    }

    [Fact]
    public async Task A_failing_test_reports_why()
    {
        var created = await ApiClient.ReadAsync<IntegrationDto>(await _api.Http.PostAsJsonAsync("/api/integrations", new SaveIntegrationRequest(
            "web-search", "Search", new Dictionary<string, string> { ["provider"] = "searxng", ["url"] = "http://127.0.0.1:9" }), ApiClient.Json));

        var result = await ApiClient.ReadAsync<IntegrationTestDto>(await _api.Http.PostAsync(new Uri($"/api/integrations/{created.Id}/test", UriKind.Relative), null));

        result.Ok.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Disabled_apps_offer_no_tools()
    {
        await _api.Http.PostAsJsonAsync("/api/integrations", new SaveIntegrationRequest(
            "home-assistant", "Home", new Dictionary<string, string> { ["url"] = "http://ha.local" }, "t", IsEnabled: false), ApiClient.Json);

        (await _api.GetAsync<List<ToolDto>>("/api/tools")).ShouldNotContain(t => t.Name.StartsWith("home_", StringComparison.Ordinal));
    }
}
