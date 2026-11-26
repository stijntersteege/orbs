using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Orbs.Application.Conversations;

namespace Orbs.Api.Endpoints;

internal static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/conversations").WithTags("Conversations");

        group.MapGet("/", (Guid? orbId, ChatService chat, CancellationToken ct) => chat.ListAsync(orbId, ct))
            .WithSummary("List recent conversations, optionally for one orb");

        group.MapGet("/{id:guid}", (Guid id, ChatService chat, CancellationToken ct) => chat.GetAsync(id, ct))
            .WithSummary("Get a conversation with its messages");

        group.MapPost("/", async (StartConversationRequest request, ChatService chat, CancellationToken ct) =>
            {
                var conversation = await chat.StartAsync(request, ct);
                return TypedResults.Created($"/api/conversations/{conversation.Id}", conversation);
            })
            .WithSummary("Start a conversation with an orb");

        group.MapDelete("/{id:guid}", async (Guid id, ChatService chat, CancellationToken ct) =>
            {
                await chat.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Delete a conversation");

        group.MapPost("/{id:guid}/messages", async (Guid id, SendMessageRequest request, ChatService chat, CancellationToken ct) =>
            {
                var events = await chat.SendAsync(id, request, ct);
                return TypedResults.ServerSentEvents(ToSse(events, ct));
            })
            .WithSummary("Send a message and stream the reply as server-sent events (start with model, provider and sources read; delta; done; error)");
    }

    private static async IAsyncEnumerable<SseItem<ChatEvent>> ToSse(
        IAsyncEnumerable<ChatEvent> events,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var e in events.WithCancellation(cancellationToken))
        {
            yield return new SseItem<ChatEvent>(e, e.Type);
        }
    }
}
