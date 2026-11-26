using System.Net;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Domain.Conversations;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ChatApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ChatApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_reply_streams_as_server_sent_events_and_is_saved()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, "Hello orb");

        result.Response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        result.Events[0].Type.ShouldBe(ChatEvent.StartType);
        result.Events[^1].Type.ShouldBe(ChatEvent.DoneType);
        result.Reply.ShouldBe("You said: Hello orb");

        var saved = await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}");
        saved.Title.ShouldBe("Hello orb");
        saved.Messages.Select(m => m.Role).ShouldBe([MessageRole.User, MessageRole.Assistant]);
    }

    [Fact]
    public async Task Without_a_model_the_api_answers_409()
    {
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, "Hello");

        result.Response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_provider_failure_mid_reply_arrives_as_an_error_event()
    {
        await _api.CreateProviderAsync(new SaveProviderRequest("Flaky", FailingModelAdapter.KindName, DefaultModel: "x"));
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, "Hello");

        result.Events[^1].Type.ShouldBe(ChatEvent.ErrorType);
        result.Events[^1].Text!.ShouldContain("provider exploded");
    }

    [Fact]
    public async Task Conversations_are_listed_per_orb()
    {
        await _api.ConnectEchoAsync();
        var pip = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var byte_ = await _api.CreateOrbAsync(new SaveOrbRequest("Byte"));
        await _api.StartConversationAsync(pip.Id);
        await _api.StartConversationAsync(byte_.Id);

        var list = await _api.GetAsync<List<ConversationSummaryDto>>($"/api/conversations?orbId={pip.Id}");

        list.ShouldHaveSingleItem().OrbId.ShouldBe(pip.Id);
    }

    [Fact]
    public async Task Deleting_an_orb_deletes_its_conversations()
    {
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        await _api.Http.DeleteAsync(new Uri($"/api/orbs/{orb.Id}", UriKind.Relative));

        (await _api.Http.GetAsync(new Uri($"/api/conversations/{chat.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
