using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Memories;
using Orbs.Application.Orbs;
using Orbs.Domain.Memories;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class MemoryApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public MemoryApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Memories_can_be_added_edited_and_forgotten()
    {
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));

        var added = await ApiClient.ReadAsync<MemoryDto>(await _api.Http.PostAsJsonAsync($"/api/orbs/{orb.Id}/memories", new SaveMemoryRequest("Sam likes tea"), ApiClient.Json));
        added.Source.ShouldBe(MemorySource.Manual);

        var edited = await ApiClient.ReadAsync<MemoryDto>(await _api.Http.PutAsJsonAsync($"/api/memories/{added.Id}", new SaveMemoryRequest("Sam likes green tea"), ApiClient.Json));
        edited.Content.ShouldBe("Sam likes green tea");

        (await _api.Http.DeleteAsync(new Uri($"/api/memories/{added.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetAsync<List<MemoryDto>>($"/api/orbs/{orb.Id}/memories")).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_orb_remembers_without_asking_and_the_memory_shows_up()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, """/tool remember {"fact":"Sam's birthday is 3 May"}""");

        result.Events.ShouldNotContain(e => e.Type == ChatEvent.ApprovalType);
        result.Reply.ShouldBe("remember said: Remembered: Sam's birthday is 3 May");
        var memory = (await _api.GetAsync<List<MemoryDto>>($"/api/orbs/{orb.Id}/memories")).ShouldHaveSingleItem();
        memory.Source.ShouldBe(MemorySource.Learned);
    }

    [Fact]
    public async Task An_orb_forgets_when_asked()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        await _api.Http.PostAsJsonAsync($"/api/orbs/{orb.Id}/memories", new SaveMemoryRequest("Sam lives in Utrecht"), ApiClient.Json);
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, """/tool forget {"memory":"Utrecht"}""");

        result.Reply.ShouldBe("forget said: Forgot: Sam lives in Utrecht");
        (await _api.GetAsync<List<MemoryDto>>($"/api/orbs/{orb.Id}/memories")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Other_people_cannot_see_an_orbs_memory()
    {
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");

        (await sam.GetAsync(new Uri($"/api/orbs/{orb.Id}/memories", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
