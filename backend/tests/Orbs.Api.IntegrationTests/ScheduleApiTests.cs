using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Application.Schedules;
using Orbs.Domain.Schedules;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ScheduleApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ScheduleApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private async Task<OrbDto> PipAsync()
    {
        await _api.ConnectEchoAsync();
        return await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
    }

    private async Task<ScheduleDto> CreateAsync(SaveScheduleRequest request) =>
        await ApiClient.ReadAsync<ScheduleDto>(await _api.Http.PostAsJsonAsync("/api/schedules", request, ApiClient.Json));

    private async Task<ConversationDto> ChatOf(Guid scheduleId)
    {
        var schedule = (await _api.GetAsync<List<ScheduleDto>>("/api/schedules")).Single(s => s.Id == scheduleId);
        return await _api.GetAsync<ConversationDto>($"/api/conversations/{schedule.ConversationId}");
    }

    [Fact]
    public async Task A_time_schedule_runs_when_its_time_comes_and_writes_into_its_own_chat()
    {
        var pip = await PipAsync();
        var schedule = await CreateAsync(new SaveScheduleRequest(pip.Id, "Weekly", "Summarize my week", TriggerKind.Time, Cron: "0 9 * * 1", TimeZone: "Europe/Amsterdam"));
        schedule.NextCheckAt.ShouldNotBeNull();

        await _factory.RunSchedulesAsync();
        schedule.ConversationId.ShouldBeNull();

        _factory.Clock.Advance(TimeSpan.FromDays(8));
        await _factory.RunSchedulesAsync();

        var chat = await ChatOf(schedule.Id);
        chat.Messages.Select(m => m.Content).ShouldBe(["[Scheduled: Weekly] Summarize my week", "You said: [Scheduled: Weekly] Summarize my week"]);
    }

    [Fact]
    public async Task A_webhook_passes_its_body_to_the_orb_and_needs_the_secret()
    {
        var pip = await PipAsync();
        var schedule = await CreateAsync(new SaveScheduleRequest(pip.Id, "Doorbell", "Tell me who rang", TriggerKind.Webhook));
        var path = schedule.WebhookPath.ShouldNotBeNull();
        (await _api.GetAsync<List<ScheduleDto>>("/api/schedules")).Single().WebhookPath.ShouldBeNull("the secret is only shown once");

        var anonymous = _factory.CreateAnonymousClient();
        (await anonymous.PostAsync(new Uri(path[..^4] + "nope", UriKind.Relative), new StringContent("x"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.PostAsync(new Uri(path, UriKind.Relative), new StringContent("Sam at the door"))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await _factory.RunJobsAsync();

        (await ChatOf(schedule.Id)).Messages[0].Content.ShouldBe("[Scheduled: Doorbell] Tell me who rang\n\nSam at the door");
    }

    [Fact]
    public async Task A_feed_runs_only_for_new_items()
    {
        var pip = await PipAsync();
        _factory.Feeds.Items.Add(new FeedItem("1", "Old post", "https://b.example/1", null));
        var schedule = await CreateAsync(new SaveScheduleRequest(pip.Id, "Blog", "Summarize new posts", TriggerKind.Rss, FeedUrl: "https://b.example/feed"));

        await _factory.RunSchedulesAsync();
        (await _api.GetAsync<List<ScheduleDto>>("/api/schedules")).Single().ConversationId.ShouldBeNull("the first look only remembers what is there");

        _factory.Feeds.Items.Insert(0, new FeedItem("2", "New post", "https://b.example/2", "Fresh"));
        _factory.Clock.Advance(Schedule.PollInterval);
        await _factory.RunSchedulesAsync();

        var message = (await ChatOf(schedule.Id)).Messages[0].Content;
        message.ShouldContain("- New post (https://b.example/2): Fresh");
        message.ShouldNotContain("Old post");
    }

    [Fact]
    public async Task Run_now_works_for_any_schedule()
    {
        var pip = await PipAsync();
        var schedule = await CreateAsync(new SaveScheduleRequest(pip.Id, "Hook", "Ping", TriggerKind.Webhook));

        (await _api.Http.PostAsync(new Uri($"/api/schedules/{schedule.Id}/run", UriKind.Relative), null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await _factory.RunJobsAsync();

        (await ChatOf(schedule.Id)).Messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Bad_cron_expressions_are_refused()
    {
        var pip = await PipAsync();

        var response = await _api.Http.PostAsJsonAsync("/api/schedules", new SaveScheduleRequest(pip.Id, "x", "y", TriggerKind.Time, Cron: "whenever"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
