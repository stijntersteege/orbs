using System.Globalization;
using System.Net.Http.Json;
using Orbs.Application.Notifications;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class NotificationSteps(SpecContext context)
{
    [Given("notifications are on for this device")]
    public async Task GivenNotificationsAreOn() =>
        (await context.Api.Http.PostAsJsonAsync("/api/push/subscriptions", new SubscribeRequest("https://push.example/me", new PushKeys("k", "a")), ApiClient.Json)).EnsureSuccessStatusCode();

    [Given("it is quiet hours right now")]
    public async Task GivenQuietHours()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new SaveNotificationSettingsRequest(now.AddHours(-1).ToString("HH:mm", CultureInfo.InvariantCulture), now.AddHours(1).ToString("HH:mm", CultureInfo.InvariantCulture), "UTC");
        (await context.Api.Http.PutAsJsonAsync("/api/account/notifications", request, ApiClient.Json)).EnsureSuccessStatusCode();
    }

    [Then("I get a notification from {string}")]
    public void ThenIGetANotification(string orb) =>
        context.Factory.Push.Sent.ShouldHaveSingleItem().Payload.ShouldContain($"\"title\":\"{orb}:");

    [Then("I get no notification yet")]
    public void ThenNoNotification() => context.Factory.Push.Sent.ShouldBeEmpty();
}
