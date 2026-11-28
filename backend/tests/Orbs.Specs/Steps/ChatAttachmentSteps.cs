using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class ChatAttachmentSteps(SpecContext context)
{
    [When("I attach a file {string} saying {string} to a chat with {string}")]
    public async Task WhenIAttach(string fileName, string text, string orb)
    {
        context.Conversation ??= await context.Api.StartConversationAsync(context.Orbs[orb].Id);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        form.Add(file, "file", fileName);
        var source = await ApiClient.ReadAsync<ContextSourceDto>(await context.Api.Http.PostAsync(new Uri("/api/context-sources/files", UriKind.Relative), form));
        (await context.Api.Http.PostAsJsonAsync($"/api/conversations/{context.Conversation.Id}/attachments", new AttachRequest(source.Id), ApiClient.Json)).EnsureSuccessStatusCode();
    }

    [Then("{string} does not keep {string} for other chats")]
    public async Task ThenDoesNotKeep(string orb, string _) =>
        (await context.Api.GetAsync<OrbDto>($"/api/orbs/{context.Orbs[orb].Id}")).ContextSourceIds.ShouldBeEmpty();
}
