using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Jobs;
using Orbs.Application.Notifications;
using Orbs.Application.Orbs;
using Orbs.Application.Schedules;
using Orbs.Domain.Jobs;
using Orbs.Domain.Schedules;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class NotificationApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public NotificationApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> SubscribeAsync(string endpoint) =>
        _api.Http.PostAsJsonAsync("/api/push/subscriptions", new SubscribeRequest(endpoint, new PushKeys("p256dh-key", "auth-key")), ApiClient.Json);

    private async Task<Guid> HookScheduleAsync()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var schedule = await ApiClient.ReadAsync<ScheduleDto>(await _api.Http.PostAsJsonAsync("/api/schedules",
            new SaveScheduleRequest(orb.Id, "Morning news", "What is new?", TriggerKind.Webhook), ApiClient.Json));
        return schedule.Id;
    }

    [Fact]
    public async Task A_schedule_reaches_out_when_it_has_written_something()
    {
        (await SubscribeAsync("https://push.example/device-1")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var scheduleId = await HookScheduleAsync();

        await _api.Http.PostAsync(new Uri($"/api/schedules/{scheduleId}/run", UriKind.Relative), null);
        await _factory.RunJobsAsync();

        var (endpoint, payload) = _factory.Push.Sent.ShouldHaveSingleItem();
        endpoint.ShouldBe("https://push.example/device-1");
        payload.ShouldContain("\"title\":\"Pip: Morning news\"");
        payload.ShouldContain("You said: [Scheduled: Morning news] What is new?");
        payload.ShouldContain("/orbs/");
    }

    [Fact]
    public async Task During_quiet_hours_notifications_wait_until_they_end()
    {
        await SubscribeAsync("https://push.example/device-1");
        var now = DateTimeOffset.UtcNow;
        var from = now.AddMinutes(-30).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var until = now.AddHours(2).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        (await _api.Http.PutAsJsonAsync("/api/account/notifications", new SaveNotificationSettingsRequest(from, until, "UTC"), ApiClient.Json)).EnsureSuccessStatusCode();
        var scheduleId = await HookScheduleAsync();

        await _api.Http.PostAsync(new Uri($"/api/schedules/{scheduleId}/run", UriKind.Relative), null);
        await _factory.RunJobsAsync();

        _factory.Push.Sent.ShouldBeEmpty();
        var waiting = (await _api.GetAsync<List<JobDto>>("/api/jobs")).Single(j => j.Kind == NotificationJob.KindName);
        waiting.Status.ShouldBe(JobStatus.Pending);
        waiting.RunAt.ShouldBeGreaterThan(now.AddHours(1));

        _factory.Clock.Advance(TimeSpan.FromHours(3));
        await _factory.RunJobsAsync();
        _factory.Push.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Devices_that_unsubscribed_are_forgotten()
    {
        await SubscribeAsync("https://push.example/gone-device");
        await SubscribeAsync("https://push.example/device-2");

        var delivered = await ApiClient.ReadAsync<Dictionary<string, int>>(await _api.Http.PostAsync(new Uri("/api/push/test", UriKind.Relative), null));

        delivered["delivered"].ShouldBe(1);
        (await _api.GetAsync<NotificationSettingsDto>("/api/account/notifications")).Devices.ShouldBe(1);
    }

    [Fact]
    public async Task Quiet_hours_need_valid_times()
    {
        var response = await _api.Http.PutAsJsonAsync("/api/account/notifications", new SaveNotificationSettingsRequest("late", "early", "UTC"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_public_key_is_there_for_browsers()
    {
        (await _api.Http.GetStringAsync(new Uri("/api/push/key", UriKind.Relative))).ShouldContain("BTestPublicKey");
    }
}
