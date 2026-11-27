using System.Net;
using Orbs.Application.Conversations;
using Orbs.Application.Providers;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class ChatSteps(SpecContext context)
{
    [Given("the demo model is connected")]
    public Task GivenTheDemoModelIsConnected() => context.Api.ConnectEchoAsync();

    [Given("a model that fails mid-reply is connected")]
    public Task GivenAFailingModelIsConnected() =>
        context.Api.CreateProviderAsync(new SaveProviderRequest("Flaky", FailingModelAdapter.KindName, DefaultModel: "x"));

    [When("I say {string} to {string}")]
    public async Task WhenISayTo(string message, string orbName)
    {
        context.Conversation ??= await context.Api.StartConversationAsync(context.Orbs[orbName].Id);
        context.LastReply = await context.Api.SendAsync(context.Conversation.Id, message);
    }

    [Then("{string} replies {string}")]
    public void ThenReplies(string orbName, string reply)
    {
        context.LastReply!.Events[^1].Type.ShouldBe(ChatEvent.DoneType, $"{orbName} should finish its reply");
        context.LastReply.Reply.ShouldBe(reply);
    }

    [Then("the conversation is titled {string}")]
    public async Task ThenTheConversationIsTitled(string title) =>
        (await ReloadConversation()).Title.ShouldBe(title);

    [Then("the conversation has {int} message(s)")]
    public async Task ThenTheConversationHasMessages(int count) =>
        (await ReloadConversation()).Messages.Count.ShouldBe(count);

    [Then("I am told to connect a model first")]
    public async Task ThenIAmToldToConnectAModel()
    {
        context.LastReply!.Response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await context.LastReply.Response.Content.ReadAsStringAsync()).ShouldContain("Connect a provider");
    }

    [Then("I see an error mentioning {string}")]
    public void ThenISeeAnErrorMentioning(string text)
    {
        var last = context.LastReply!.Events[^1];
        last.Type.ShouldBe(ChatEvent.ErrorType);
        last.Text!.ShouldContain(text);
    }

    [Then("the reply was written by {string} from {string}")]
    public void ThenTheReplyWasWrittenBy(string model, string provider)
    {
        var start = context.LastReply!.Events[0];
        start.Type.ShouldBe(ChatEvent.StartType);
        start.Model.ShouldBe(model);
        start.Provider.ShouldBe(provider);
    }

    [Then("the reply read the source {string}")]
    public void ThenTheReplyReadTheSource(string name) =>
        context.LastReply!.Events[0].Sources!.Select(s => s.Name).ShouldContain(name);

    [Then("the chat opens with the {int} newest messages when it shows {int} at a time")]
    public async Task ThenTheChatOpensWithNewest(int count, int pageSize)
    {
        var conversation = await context.Api.GetAsync<ConversationDto>($"/api/conversations/{context.Conversation!.Id}?limit={pageSize}");
        conversation.Messages.Count.ShouldBe(count);
        conversation.HasMoreMessages.ShouldBeTrue();
        context.LastPageStart = conversation.Messages[0].CreatedAt;
    }

    [Then("paging back shows {string} and {string}")]
    public async Task ThenPagingBackShows(string first, string second)
    {
        var before = Uri.EscapeDataString(context.LastPageStart!.Value.ToString("O"));
        var page = await context.Api.GetAsync<MessagePageDto>($"/api/conversations/{context.Conversation!.Id}/messages?before={before}");
        page.Messages.Select(m => m.Content).ShouldBe([first, second]);
        page.HasMore.ShouldBeFalse();
    }

    [Then("the reply used the tool {string} with the result {string}")]
    public void ThenTheReplyUsedTheTool(string tool, string result)
    {
        context.LastReply!.Events.ShouldContain(e => e.Type == ChatEvent.ToolCallType && e.Tool == tool);
        context.LastReply.Events.ShouldContain(e => e.Type == ChatEvent.ToolResultType && e.Text == result);
    }

    [Then("the last saved reply lists the tool {string}")]
    public async Task ThenTheLastSavedReplyListsTheTool(string tool) =>
        (await ReloadConversation()).Messages[^1].ToolCalls.ShouldNotBeNull().Select(t => t.Tool).ShouldContain(tool);

    private Task<ConversationDto> ReloadConversation() =>
        context.Api.GetAsync<ConversationDto>($"/api/conversations/{context.Conversation!.Id}");
}
