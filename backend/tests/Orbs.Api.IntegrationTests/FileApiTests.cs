using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Orbs.Application.Context;
using Orbs.Application.Orbs;
using Orbs.Domain.Context;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class FileApiTests : IDisposable
{
    private static readonly byte[] TinyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public FileApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, byte[] data, string contentType, string? name = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        if (name is not null)
        {
            form.Add(new StringContent(name), "name");
        }

        return await client.PostAsync(new Uri("/api/context-sources/files", UriKind.Relative), form);
    }

    [Fact]
    public async Task A_markdown_file_becomes_a_ready_source_and_can_be_downloaded()
    {
        var bytes = Encoding.UTF8.GetBytes("# Plan\n\nShip it on Friday.");

        var source = await ApiClient.ReadAsync<ContextSourceDto>(await UploadAsync(_api.Http, "plan.md", bytes, "text/markdown"));

        source.Name.ShouldBe("plan");
        source.Kind.ShouldBe("file");
        source.Status.ShouldBe(ContextSourceStatus.Ready);
        source.Location.ShouldBe("plan.md");
        source.ContentLength.ShouldBe(26);
        (await _api.Http.GetByteArrayAsync(new Uri($"/api/context-sources/{source.Id}/file", UriKind.Relative))).ShouldBe(bytes);
    }

    [Fact]
    public async Task Unsupported_files_are_refused()
    {
        var response = await UploadAsync(_api.Http, "setup.exe", [1, 2, 3], "application/octet-stream");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("cannot read .exe");
    }

    [Fact]
    public async Task Files_cannot_be_added_through_the_text_endpoint()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/context-sources", new SaveContextSourceRequest("x", "file", Input: "x"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_orb_with_a_picture_shows_it_to_models_that_can_see()
    {
        await _api.ConnectEchoAsync();
        var image = await ApiClient.ReadAsync<ContextSourceDto>(await UploadAsync(_api.Http, "whiteboard.png", TinyPng, "image/png", "Whiteboard"));
        image.Status.ShouldBe(ContextSourceStatus.Ready);
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip", ContextSourceIds: [image.Id]));
        var chat = await _api.StartConversationAsync(orb.Id);

        var reply = await _api.SendAsync(chat.Id, "What is on it?");

        reply.Reply.ShouldBe("You said: What is on it? (and showed me 1 picture)");
    }

    [Fact]
    public async Task Other_people_cannot_download_a_file()
    {
        var source = await ApiClient.ReadAsync<ContextSourceDto>(await UploadAsync(_api.Http, "secret.txt", Encoding.UTF8.GetBytes("secret"), "text/plain"));
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");

        (await sam.GetAsync(new Uri($"/api/context-sources/{source.Id}/file", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
