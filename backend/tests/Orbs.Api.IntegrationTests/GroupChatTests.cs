using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class GroupChatTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public GroupChatTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Mentioning_another_orb_hands_it_the_question()
    {
        await _api.ConnectEchoAsync();
        var pip = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var nova = await _api.CreateOrbAsync(new SaveOrbRequest("Nova"));
        var chat = await _api.StartConversationAsync(pip.Id);

        var result = await _api.SendAsync(chat.Id, "@Nova what do you think?");

        var start = result.Events[0];
        start.OrbId.ShouldBe(nova.Id);
        start.OrbName.ShouldBe("Nova");
        var saved = await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}");
        saved.OrbIds.ShouldBe([pip.Id, nova.Id]);
        saved.Messages[^1].OrbId.ShouldBe(nova.Id);
        (await _api.GetAsync<List<ConversationSummaryDto>>($"/api/conversations?orbId={nova.Id}")).Select(c => c.Id).ShouldContain(chat.Id);
    }

    [Fact]
    public async Task Several_mentioned_orbs_answer_in_turn_and_hear_each_other()
    {
        await _api.ConnectEchoAsync();
        var pip = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        await _api.CreateOrbAsync(new SaveOrbRequest("Nova"));
        var chat = await _api.StartConversationAsync(pip.Id);

        var result = await _api.SendAsync(chat.Id, "@Pip and @Nova: hello");

        result.Events.Where(e => e.Type == ChatEvent.StartType).Select(e => e.OrbName).ShouldBe(["Pip", "Nova"]);
        result.Events.Count(e => e.Type == ChatEvent.DoneType).ShouldBe(2);
        var messages = (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).Messages;
        messages.Count.ShouldBe(3);
        messages[2].Content.ShouldBe("You said: Pip said: You said: @Pip and @Nova: hello", "Nova saw what Pip said");
    }

    [Fact]
    public async Task Without_a_mention_the_chats_own_orb_answers()
    {
        await _api.ConnectEchoAsync();
        var pip = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var nova = await _api.CreateOrbAsync(new SaveOrbRequest("Nova"));
        var group = await ApiClient.ReadAsync<ConversationDto>(await _api.Http.PostAsJsonAsync("/api/conversations", new StartConversationRequest(pip.Id, [nova.Id]), ApiClient.Json));
        group.OrbIds.ShouldBe([pip.Id, nova.Id]);

        var result = await _api.SendAsync(group.Id, "hi all");

        result.Events[0].OrbName.ShouldBe("Pip");
    }
}
