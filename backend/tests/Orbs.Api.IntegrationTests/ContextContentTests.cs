using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Orbs.Application.Context;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ContextContentTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ContextContentTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_note_shows_its_text_and_can_be_edited_as_a_note()
    {
        var note = await _api.AddNoteAsync("Roadmap", "Ship voice in May.");

        var content = await _api.GetAsync<ContextContentDto>($"/api/context-sources/{note.Id}/content");

        content.Content.ShouldBe("Ship voice in May.");
        content.Editing.ShouldBe(ContextEditing.Note);
        content.FileName.ShouldBeNull();
    }

    [Fact]
    public async Task A_text_file_shows_its_contents_and_saves_edits_back_into_the_file()
    {
        var file = await UploadAsync(_api.Http, "plan.md", "text/markdown", Encoding.UTF8.GetBytes("# Plan\n\nShip on Friday."));

        var content = await _api.GetAsync<ContextContentDto>($"/api/context-sources/{file.Id}/content");
        content.Content.ShouldBe("# Plan\n\nShip on Friday.");
        content.Editing.ShouldBe(ContextEditing.TextFile);
        content.FileName.ShouldBe("plan.md");
        content.ContentType.ShouldBe("text/markdown");

        var saved = await ApiClient.ReadAsync<ContextContentDto>(await _api.Http.PutAsJsonAsync($"/api/context-sources/{file.Id}/file", new ReplaceFileTextRequest("# Plan\n\nShip on Monday."), ApiClient.Json));

        saved.Content.ShouldBe("# Plan\n\nShip on Monday.");
        var download = await _api.Http.GetStringAsync(new Uri($"/api/context-sources/{file.Id}/file", UriKind.Relative));
        download.ShouldBe("# Plan\n\nShip on Monday.");
        (await _api.GetAsync<ContextSourceDto>($"/api/context-sources/{file.Id}")).ContentLength.ShouldBe("# Plan\n\nShip on Monday.".Length);
    }

    [Fact]
    public async Task A_picture_is_shown_but_not_edited_as_text()
    {
        var picture = await UploadAsync(_api.Http, "face.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);

        var content = await _api.GetAsync<ContextContentDto>($"/api/context-sources/{picture.Id}/content");
        content.Editing.ShouldBe(ContextEditing.NameOnly);
        content.ContentType.ShouldBe("image/png");
        content.Size.ShouldBe(7);

        var edit = await _api.Http.PutAsJsonAsync($"/api/context-sources/{picture.Id}/file", new ReplaceFileTextRequest("hacked"), ApiClient.Json);
        edit.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Other_people_cannot_read_or_edit_your_files()
    {
        var file = await UploadAsync(_api.Http, "plan.md", "text/markdown", Encoding.UTF8.GetBytes("Secret plan"));
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");

        (await sam.GetAsync(new Uri($"/api/context-sources/{file.Id}/content", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await sam.PutAsJsonAsync($"/api/context-sources/{file.Id}/file", new ReplaceFileTextRequest("mine"), ApiClient.Json)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<ContextSourceDto> UploadAsync(HttpClient client, string fileName, string contentType, byte[] data)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await ApiClient.ReadAsync<ContextSourceDto>(await client.PostAsync(new Uri("/api/context-sources/files", UriKind.Relative), form));
    }
}
