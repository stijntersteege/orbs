using System.Net;
using System.Net.Http.Headers;
using Orbs.Application.Context;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Domain.Context;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ProviderAndContextApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ProviderAndContextApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Provider_kinds_list_every_registered_adapter()
    {
        var kinds = await _api.GetAsync<List<ProviderKindDto>>("/api/providers/kinds");

        kinds.Select(k => k.Kind).ShouldContain("echo");
        kinds.Select(k => k.Kind).ShouldContain("openai-compatible");
    }

    [Fact]
    public async Task Api_keys_never_leave_the_server()
    {
        await _api.CreateProviderAsync(new SaveProviderRequest("Lab", "openai-compatible", "http://localhost:1/v1", ApiKey: "sk-super-secret"));

        var json = await _api.Http.GetStringAsync(new Uri("/api/providers", UriKind.Relative));

        json.ShouldNotContain("sk-super-secret");
        json.ShouldContain("\"hasApiKey\":true");
    }

    [Fact]
    public async Task An_unreachable_provider_answers_502_when_listing_models()
    {
        var provider = await _api.CreateProviderAsync(new SaveProviderRequest("Flaky", FailingModelAdapter.KindName));

        var response = await _api.Http.GetAsync(new Uri($"/api/providers/{provider.Id}/models", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Notes_are_ready_at_once_and_can_be_linked_to_orbs()
    {
        var note = await _api.AddNoteAsync("Team", "Sam leads design.");
        note.Status.ShouldBe(ContextSourceStatus.Ready);

        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip", ContextSourceIds: [note.Id]));

        orb.ContextSourceIds.ShouldBe([note.Id]);
    }

    [Fact]
    public async Task Context_kinds_list_every_registered_connector()
    {
        var kinds = await _api.GetAsync<List<ContextKindDto>>("/api/context-sources/kinds");

        kinds.Select(k => k.Kind).ShouldBe(["text", "url"], ignoreOrder: true);
    }
}

public sealed class HostingTests
{
    [Fact]
    public async Task Health_reports_healthy()
    {
        using var factory = new OrbsApiFactory();

        (await factory.CreateClient().GetStringAsync(new Uri("/health", UriKind.Relative))).ShouldBe("Healthy");
    }

    [Fact]
    public async Task The_openapi_document_describes_the_api()
    {
        using var factory = new OrbsApiFactory();

        var json = await factory.CreateClient().GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        json.ShouldContain("/api/orbs");
        json.ShouldContain("/api/conversations/{id}/messages");
    }

    [Fact]
    public async Task With_an_api_token_requests_without_it_are_refused()
    {
        using var factory = new OrbsApiFactory { ApiToken = "letmein" };
        var client = factory.CreateClient();

        (await client.GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "letmein");
        (await client.GetAsync(new Uri("/api/orbs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_demo_seed_gives_a_first_run_an_orb_that_can_talk()
    {
        using var factory = new OrbsApiFactory { SeedDemoData = true };
        var api = new ApiClient(factory.CreateClient());

        var orbs = await api.GetAsync<List<OrbDto>>("/api/orbs");
        var chat = await api.StartConversationAsync(orbs.ShouldHaveSingleItem().Id);

        (await api.SendAsync(chat.Id, "hi")).Reply.ShouldBe("You said: hi");
    }
}
