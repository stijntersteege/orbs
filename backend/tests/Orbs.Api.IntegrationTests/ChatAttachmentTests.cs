using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ChatAttachmentTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ChatAttachmentTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_file_attached_to_a_chat_is_read_there_and_nowhere_else()
    {
        var (orb, chat) = await StartAsync();
        var file = await UploadAsync(_api.Http, "menu.md", "# Menu\n\nSoup on Friday.");

        var attached = await _api.Http.PostAsJsonAsync($"/api/conversations/{chat.Id}/attachments", new AttachRequest(file.Id), ApiClient.Json);
        attached.EnsureSuccessStatusCode();

        var here = await _api.SendAsync(chat.Id, "What is on the menu?");
        here.Events[0].Sources.ShouldNotBeNull().Select(s => s.Name).ShouldContain("menu");
        (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).AttachedSourceIds.ShouldBe([file.Id]);

        var other = await _api.StartConversationAsync(orb.Id);
        var elsewhere = await _api.SendAsync(other.Id, "What is on the menu?");
        (elsewhere.Events[0].Sources ?? []).ShouldBeEmpty();
        (await _api.GetAsync<OrbDto>($"/api/orbs/{orb.Id}")).ContextSourceIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remembering_a_file_gives_it_to_the_orb_for_every_chat()
    {
        var (orb, chat) = await StartAsync();
        var file = await UploadAsync(_api.Http, "menu.md", "Soup on Friday.");

        (await _api.Http.PostAsJsonAsync($"/api/conversations/{chat.Id}/attachments", new AttachRequest(file.Id, Remember: true), ApiClient.Json)).EnsureSuccessStatusCode();

        (await _api.GetAsync<OrbDto>($"/api/orbs/{orb.Id}")).ContextSourceIds.ShouldBe([file.Id]);
        var other = await _api.StartConversationAsync(orb.Id);
        (await _api.SendAsync(other.Id, "menu?")).Events[0].Sources.ShouldNotBeNull().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task A_file_can_be_taken_out_of_a_chat()
    {
        var (_, chat) = await StartAsync();
        var file = await UploadAsync(_api.Http, "menu.md", "Soup on Friday.");
        await _api.Http.PostAsJsonAsync($"/api/conversations/{chat.Id}/attachments", new AttachRequest(file.Id), ApiClient.Json);

        var response = await _api.Http.DeleteAsync(new Uri($"/api/conversations/{chat.Id}/attachments/{file.Id}", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).AttachedSourceIds.ShouldBeEmpty();
        (await _api.GetAsync<ContextSourceDto>($"/api/context-sources/{file.Id}")).Id.ShouldBe(file.Id, "the file itself stays under Context");
    }

    [Fact]
    public async Task Someone_else_s_file_cannot_be_attached()
    {
        var (_, chat) = await StartAsync();
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");
        var samsFile = await UploadAsync(sam, "secret.md", "Sam's secret.");

        var response = await _api.Http.PostAsJsonAsync($"/api/conversations/{chat.Id}/attachments", new AttachRequest(samsFile.Id), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(OrbDto Orb, ConversationDto Chat)> StartAsync()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        return (orb, await _api.StartConversationAsync(orb.Id));
    }

    private static async Task<ContextSourceDto> UploadAsync(HttpClient client, string fileName, string text)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        form.Add(file, "file", fileName);
        return await ApiClient.ReadAsync<ContextSourceDto>(await client.PostAsync(new Uri("/api/context-sources/files", UriKind.Relative), form));
    }
}
