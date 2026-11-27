using Orbs.Application.Conversations;
using Orbs.Specs.Support;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class GroupChatSteps(SpecContext context)
{
    [Then("{string} answers in the chat")]
    public void ThenAnswers(string orb) =>
        context.LastReply!.Events.ShouldContain(e => e.Type == ChatEvent.StartType && e.OrbName == orb);

    [Then("the chat has {string} and {string} in it")]
    public async Task ThenTheChatHas(string first, string second)
    {
        var chat = await context.Api.GetAsync<ConversationDto>($"/api/conversations/{context.Conversation!.Id}");
        chat.OrbIds.ShouldBe([context.Orbs[first].Id, context.Orbs[second].Id]);
    }
}
