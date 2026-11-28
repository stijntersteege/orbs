using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ManagingChatsTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ManagingChatsTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_chat_can_be_renamed()
    {
        var chat = await StartChatAsync();

        var renamed = await ApiClient.ReadAsync<ConversationSummaryDto>(await _api.Http.PutAsJsonAsync($"/api/conversations/{chat.Id}", new RenameConversationRequest("  Trip plans  "), ApiClient.Json));

        renamed.Title.ShouldBe("Trip plans");
        (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).Title.ShouldBe("Trip plans");
    }

    [Fact]
    public async Task An_empty_title_is_refused()
    {
        var chat = await StartChatAsync();

        var response = await _api.Http.PutAsJsonAsync($"/api/conversations/{chat.Id}", new RenameConversationRequest(" "), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task One_message_can_be_deleted_and_the_orb_no_longer_sees_it()
    {
        var chat = await StartChatAsync();
        await _api.SendAsync(chat.Id, "first");
        await _api.SendAsync(chat.Id, "second");
        var before = (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).Messages;
        var reply = before.Single(m => m.Content == "You said: first");

        var response = await _api.Http.DeleteAsync(new Uri($"/api/conversations/{chat.Id}/messages/{reply.Id}", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var after = (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).Messages;
        after.Select(m => m.Content).ShouldBe(["first", "second", "You said: second"]);
    }

    [Fact]
    public async Task A_message_older_than_the_loaded_history_can_be_deleted()
    {
        var chat = await StartChatAsync();
        for (var i = 0; i < 55; i++)
        {
            await _api.SendAsync(chat.Id, $"message {i}");
            _factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        var oldest = (await _api.GetAsync<MessagePageDto>($"/api/conversations/{chat.Id}/messages?limit=200")).Messages[0];

        (await _api.Http.DeleteAsync(new Uri($"/api/conversations/{chat.Id}/messages/{oldest.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetAsync<MessagePageDto>($"/api/conversations/{chat.Id}/messages?limit=200")).Messages.ShouldNotContain(m => m.Id == oldest.Id);
    }

    [Fact]
    public async Task Other_people_cannot_rename_or_change_your_chats()
    {
        var chat = await StartChatAsync();
        await _api.SendAsync(chat.Id, "secret");
        var message = (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).Messages[0];
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");

        (await sam.PutAsJsonAsync($"/api/conversations/{chat.Id}", new RenameConversationRequest("Mine"), ApiClient.Json)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await sam.DeleteAsync(new Uri($"/api/conversations/{chat.Id}/messages/{message.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<ConversationDto> StartChatAsync()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        return await _api.StartConversationAsync(orb.Id);
    }
}
