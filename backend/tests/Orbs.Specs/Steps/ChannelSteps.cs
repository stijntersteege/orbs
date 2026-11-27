using System.Net.Http.Json;
using Orbs.Application.Channels;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class ChannelSteps(SpecContext context)
{
    private string? _code;

    [Given("{string} is connected to the test chat app")]
    public async Task GivenConnected(string orb) =>
        _code = (await ApiClient.ReadAsync<ChannelDto>(await context.Api.Http.PostAsJsonAsync("/api/channels",
            new SaveChannelRequest(context.Orbs[orb].Id, FakeChannel.KindName, Secret: "token"), ApiClient.Json))).PairingCode;

    [When("{string} sends the pairing code")]
    public Task WhenSendsTheCode(string who) => Send(who, "/start " + _code);

    [When("{string} sends {string}")]
    public Task WhenSends(string who, string text) => Send(who, text);

    [Then("{string} gets the answer {string}")]
    public void ThenGetsTheAnswer(string who, string text) =>
        context.Factory.Chat.Outbox[^1].ShouldBe((who, text));

    [Then("{string} gets no answer")]
    public void ThenGetsNoAnswer(string who) =>
        context.Factory.Chat.Outbox.ShouldNotContain(m => m.ChatId == who);

    private async Task Send(string who, string text)
    {
        context.Factory.Chat.Inbox.Enqueue(new ChannelMessage(who, who, text));
        await context.Factory.RunChannelsAsync();
    }
}
