using Orbs.Application.Accounts;
using Orbs.Application.Schedules;

namespace Orbs.Api.Endpoints;

internal static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/schedules").WithTags("Schedules");

        group.MapGet("/", (Guid? orbId, ScheduleService schedules, CancellationToken ct) => schedules.ListAsync(orbId, ct))
            .WithSummary("Your schedules and triggers, optionally for one orb");

        group.MapPost("/", async (SaveScheduleRequest request, ScheduleService schedules, CancellationToken ct) =>
            {
                var schedule = await schedules.CreateAsync(request, ct);
                return TypedResults.Created($"/api/schedules/{schedule.Id}", schedule);
            })
            .WithSummary("Create a schedule: a time (cron), a webhook, an RSS feed or new email. Webhooks get their secret path once");

        group.MapPut("/{id:guid}", (Guid id, SaveScheduleRequest request, ScheduleService schedules, CancellationToken ct) => schedules.UpdateAsync(id, request, ct))
            .WithSummary("Change a schedule");

        group.MapDelete("/{id:guid}", async (Guid id, ScheduleService schedules, CancellationToken ct) =>
            {
                await schedules.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Delete a schedule");

        group.MapPost("/{id:guid}/run", async (Guid id, ScheduleService schedules, CancellationToken ct) =>
            {
                await schedules.RunNowAsync(id, ct);
                return TypedResults.Accepted((string?)null);
            })
            .WithSummary("Run a schedule now, in the background");

        api.MapPost("/hooks/{id:guid}/{secret}", async (Guid id, string secret, HttpRequest request, ScheduleService schedules, CurrentUser currentUser, CancellationToken ct) =>
            {
                // Nobody signs in to call a hook: the secret is the credential, so look it up as the system.
                currentUser.ActAsSystem();
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                return await schedules.ReceiveWebhookAsync(id, secret, body, ct) ? Results.Accepted() : Results.NotFound();
            })
            .WithTags("Schedules")
            .WithSummary("Trigger a webhook schedule. The request body (up to 10,000 characters) goes to the orb with the prompt");
    }
}
