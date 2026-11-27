using Orbs.Application.Channels;

namespace Orbs.Api.Endpoints;

internal static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/channels").WithTags("Channels");

        group.MapGet("/kinds", (ChannelService channels) => channels.Kinds())
            .WithSummary("Messaging apps orbs can live in, and what each needs");

        group.MapGet("/", (ChannelService channels, CancellationToken ct) => channels.ListAsync(ct))
            .WithSummary("Your orbs' channels and their paired chats");

        group.MapPost("/", async (SaveChannelRequest request, ChannelService channels, CancellationToken ct) =>
            {
                var channel = await channels.CreateAsync(request, ct);
                return TypedResults.Created($"/api/channels/{channel.Id}", channel);
            })
            .WithSummary("Connect an orb to a messaging app. The answer holds a one-time pairing code");

        group.MapPut("/{id:guid}", (Guid id, SaveChannelRequest request, ChannelService channels, CancellationToken ct) => channels.UpdateAsync(id, request, ct))
            .WithSummary("Change a channel's settings, or switch it off");

        group.MapPost("/{id:guid}/pairing-code", (Guid id, ChannelService channels, CancellationToken ct) => channels.NewPairingCodeAsync(id, ct))
            .WithSummary("A new one-time code, to pair another chat");

        group.MapDelete("/{id:guid}/chats/{chatId:guid}", async (Guid id, Guid chatId, ChannelService channels, CancellationToken ct) =>
            {
                await channels.UnpairAsync(id, chatId, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Stop answering a chat");

        group.MapPut("/{id:guid}/avatar", async (Guid id, HttpRequest request, ChannelService channels, CancellationToken ct) =>
            {
                using var buffer = new MemoryStream();
                await request.Body.CopyToAsync(buffer, ct);
                return new { note = await channels.SetAvatarAsync(id, buffer.ToArray(), ct) };
            })
            .WithSummary("Use a PNG of the orb's face as the bot's picture, where the app allows it");

        group.MapDelete("/{id:guid}", async (Guid id, ChannelService channels, CancellationToken ct) =>
            {
                await channels.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Disconnect a channel");
    }
}
