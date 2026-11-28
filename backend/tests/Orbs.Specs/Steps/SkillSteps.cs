using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Skills;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class SkillSteps(SpecContext context)
{
    [When("I save that reply as the skill {string}")]
    public async Task WhenISaveThatReply(string name)
    {
        var messageId = context.LastReply!.Events.Single(e => e.Type == ChatEvent.DoneType).MessageId;
        var response = await context.Api.Http.PostAsJsonAsync(
            $"/api/conversations/{context.Conversation!.Id}/messages/{messageId}/skill",
            new SkillFromReplyRequest(name),
            ApiClient.Json);
        _ = await ApiClient.ReadAsync<SkillDto>(response);
    }

    [Then("the reply used the tool {string} with a result mentioning {string}")]
    public void ThenTheReplyUsedTheToolWithAResultMentioning(string tool, string text) =>
        context.LastReply!.Events.ShouldContain(e => e.Type == ChatEvent.ToolResultType && e.Tool == tool && e.Text!.Contains(text, StringComparison.Ordinal));
}
