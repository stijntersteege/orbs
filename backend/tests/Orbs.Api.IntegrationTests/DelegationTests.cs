using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class DelegationTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public DelegationTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_coordinator_asks_a_specialist_without_asking_the_person_first()
    {
        await _api.ConnectEchoAsync();
        var lead = await _api.CreateOrbAsync(new SaveOrbRequest("Lead", Instructions: "Coordinate the others."));
        await _api.CreateOrbAsync(new SaveOrbRequest("Chef", "Knows recipes"));
        var chat = await _api.StartConversationAsync(lead.Id);

        var result = await _api.SendAsync(chat.Id, """/tool ask_orb {"orb":"chef","question":"Dinner for four?"}""");

        result.Events.ShouldNotContain(e => e.Type == ChatEvent.ApprovalType);
        result.Reply.ShouldBe("ask_orb said: Chef says: You said: Dinner for four?");
        var saved = await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}");
        saved.OrbIds.ShouldBe([lead.Id], "the specialist answers on the side and does not join the chat");
    }

    [Fact]
    public async Task Without_an_orb_the_tool_lists_the_others()
    {
        await _api.ConnectEchoAsync();
        var lead = await _api.CreateOrbAsync(new SaveOrbRequest("Lead"));
        await _api.CreateOrbAsync(new SaveOrbRequest("Chef", "Knows recipes"));
        await _api.CreateOrbAsync(new SaveOrbRequest("Coach", Instructions: "Plans workouts.\nKeep it short."));
        var chat = await _api.StartConversationAsync(lead.Id);

        var result = await _api.SendAsync(chat.Id, "/tool ask_orb {}");

        result.Reply.ShouldContain("- Chef: Knows recipes");
        result.Reply.ShouldContain("- Coach: Plans workouts.");
        result.Reply.ShouldNotContain("- Lead");
    }

    [Fact]
    public async Task Asking_an_unknown_orb_reports_an_error_to_the_model()
    {
        await _api.ConnectEchoAsync();
        var lead = await _api.CreateOrbAsync(new SaveOrbRequest("Lead"));
        var chat = await _api.StartConversationAsync(lead.Id);

        var result = await _api.SendAsync(chat.Id, """/tool ask_orb {"orb":"Lead","question":"hi"}""");

        result.Events.ShouldContain(e => e.Type == ChatEvent.ToolResultType && e.IsError == true);
        result.Reply.ShouldContain("no other orb named");
    }
}
