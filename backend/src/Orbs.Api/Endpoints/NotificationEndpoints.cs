using Orbs.Application.Notifications;

namespace Orbs.Api.Endpoints;

internal static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder api)
    {
        var push = api.MapGroup("/push").WithTags("Notifications");

        push.MapGet("/key", (NotificationService notifications) => new { publicKey = notifications.PublicKey })
            .WithSummary("The VAPID public key browsers need to subscribe to notifications");

        push.MapPost("/subscriptions", async (SubscribeRequest request, HttpContext http, NotificationService notifications, CancellationToken ct) =>
            {
                await notifications.SubscribeAsync(request, http.Request.Headers.UserAgent.ToString(), ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Get notifications on this browser (a PushSubscription from the Push API)");

        push.MapDelete("/subscriptions", async (string endpoint, NotificationService notifications, CancellationToken ct) =>
            {
                await notifications.UnsubscribeAsync(endpoint, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Stop notifications on a browser");

        push.MapPost("/test", async (NotificationService notifications, CancellationToken ct) => new { delivered = await notifications.TestAsync(ct) })
            .WithSummary("Send a test notification to all your devices");

        api.MapGet("/account/notifications", (NotificationService notifications, CancellationToken ct) => notifications.GetSettingsAsync(ct))
            .WithTags("Notifications")
            .WithSummary("Quiet hours and how many devices get notifications");

        api.MapPut("/account/notifications", (SaveNotificationSettingsRequest request, NotificationService notifications, CancellationToken ct) => notifications.SaveSettingsAsync(request, ct))
            .WithTags("Notifications")
            .WithSummary("Set quiet hours (HH:mm to HH:mm in a time zone), or clear them");
    }
}
