using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class ManagingChatSteps(SpecContext context)
{
    [When("I rename the conversation to {string}")]
    public async Task WhenIRename(string title) =>
        (await context.Api.Http.PutAsJsonAsync($"/api/conversations/{context.Conversation!.Id}", new RenameConversationRequest(title), ApiClient.Json)).EnsureSuccessStatusCode();

    [When("I delete the last reply")]
    public async Task WhenIDeleteTheLastReply()
    {
        var chat = await context.Api.GetAsync<ConversationDto>($"/api/conversations/{context.Conversation!.Id}");
        var reply = chat.Messages[^1];
        (await context.Api.Http.DeleteAsync(new Uri($"/api/conversations/{chat.Id}/messages/{reply.Id}", UriKind.Relative))).EnsureSuccessStatusCode();
    }
}
