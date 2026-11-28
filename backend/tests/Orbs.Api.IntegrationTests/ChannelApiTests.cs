using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Channels;
using Orbs.Application.Orbs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ChannelApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ChannelApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private async Task<ChannelDto> ConnectAsync()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        return await ApiClient.ReadAsync<ChannelDto>(await _api.Http.PostAsJsonAsync("/api/channels", new SaveChannelRequest(orb.Id, FakeChannel.KindName, Secret: "token"), ApiClient.Json));
    }

    private async Task SayAsync(string chatId, string text)
    {
        _factory.Chat.Inbox.Enqueue(new ChannelMessage(chatId, "Sam", text));
        await _factory.RunChannelsAsync();
    }

    [Fact]
    public async Task Strangers_are_ignored_until_a_chat_pairs_with_the_code()
    {
        var channel = await ConnectAsync();
        channel.Address.ShouldBe("@test_bot");
        var code = channel.PairingCode.ShouldNotBeNull();

        await SayAsync("stranger", "hello?");
        _factory.Chat.Outbox.ShouldBeEmpty();

        await SayAsync("sam", $"/start {code.ToLowerInvariant()}");
        _factory.Chat.Outbox.ShouldHaveSingleItem().Text.ShouldBe("Paired. You are talking to Pip now.");

        await SayAsync("sam", "Hello Pip");
        _factory.Chat.Outbox[^1].ShouldBe(("sam", "You said: Hello Pip"));

        await SayAsync("intruder", $"/start {code}");
        _factory.Chat.Outbox.Count.ShouldBe(2, "a pairing code works only once");
    }

    [Fact]
    public async Task Approvals_are_answered_in_the_chat_with_yes_or_no()
    {
        var channel = await ConnectAsync();
        await SayAsync("sam", channel.PairingCode!);

        await SayAsync("sam", """/tool calculator {"expression":"6*7"}""");
        _factory.Chat.Outbox[^1].Text.ShouldStartWith("Pip wants to use calculator");

        await SayAsync("sam", "yes");
        _factory.Chat.Outbox[^1].Text.ShouldBe("calculator said: 42");
    }

    [Fact]
    public async Task The_chat_continues_one_conversation_visible_in_the_app()
    {
        var channel = await ConnectAsync();
        await SayAsync("sam", channel.PairingCode!);
        await SayAsync("sam", "one");
        await SayAsync("sam", "two");

        var chat = (await _api.GetAsync<List<ChannelDto>>("/api/channels")).Single().Chats.ShouldHaveSingleItem();
        var conversation = await _api.GetAsync<Orbs.Application.Conversations.ConversationDto>($"/api/conversations/{chat.ConversationId}");
        conversation.Messages.Count.ShouldBe(4);
    }

    [Fact]
    public async Task A_bad_token_is_refused_when_connecting()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));

        var response = await _api.Http.PostAsJsonAsync("/api/channels", new SaveChannelRequest(orb.Id, FakeChannel.KindName, Secret: "bad"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Unauthorized");
    }

    [Fact]
    public async Task Telegram_is_offered()
    {
        (await _api.GetAsync<List<ChannelKindDto>>("/api/channels/kinds")).Select(k => k.Kind).ShouldContain("telegram");
    }
}
