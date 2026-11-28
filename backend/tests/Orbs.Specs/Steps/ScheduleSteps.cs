using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Schedules;
using Orbs.Domain.Schedules;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class ScheduleSteps(SpecContext context)
{
    private string? _webhookPath;

    [Given("{string} summarizes my week every Monday at 9:00")]
    public Task GivenWeekly(string orb) =>
        Create(new SaveScheduleRequest(context.Orbs[orb].Id, "Weekly summary", "Summarize my week", TriggerKind.Time, Cron: "0 9 * * 1"));

    [Given("{string} listens for a webhook called {string}")]
    public async Task GivenWebhook(string orb, string name) =>
        _webhookPath = (await Create(new SaveScheduleRequest(context.Orbs[orb].Id, name, "Tell me what happened", TriggerKind.Webhook))).WebhookPath;

    [When("a week goes by")]
    public async Task WhenAWeekGoesBy()
    {
        context.Factory.Clock.Advance(TimeSpan.FromDays(8));
        await context.Factory.RunSchedulesAsync();
    }

    [When("the webhook is called with {string}")]
    public async Task WhenTheWebhookIsCalled(string body)
    {
        (await context.Factory.CreateAnonymousClient().PostAsync(new Uri(_webhookPath!, UriKind.Relative), new StringContent(body))).EnsureSuccessStatusCode();
        await context.Factory.RunJobsAsync();
    }

    [Then("{string} has written in the {string} chat")]
    public async Task ThenHasWritten(string orb, string schedule) =>
        (await ChatOf(schedule)).Messages.ShouldContain(m => m.Role == Orbs.Domain.Conversations.MessageRole.Assistant, $"{orb} should have answered");

    [Then("the {string} chat mentions {string}")]
    public async Task ThenTheChatMentions(string schedule, string text) =>
        (await ChatOf(schedule)).Messages[0].Content.ShouldContain(text);

    private async Task<ScheduleDto> Create(SaveScheduleRequest request) =>
        await ApiClient.ReadAsync<ScheduleDto>(await context.Api.Http.PostAsJsonAsync("/api/schedules", request, ApiClient.Json));

    private async Task<ConversationDto> ChatOf(string name)
    {
        var schedule = (await context.Api.GetAsync<List<ScheduleDto>>("/api/schedules")).Single(s => s.Name == name);
        return await context.Api.GetAsync<ConversationDto>($"/api/conversations/{schedule.ConversationId}");
    }
}
