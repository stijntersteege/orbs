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

        group.MapGet("/{id:guid}", (Guid id, int? limit, ChatService chat, CancellationToken ct) => chat.GetAsync(id, limit, ct))
            .WithSummary("Get a conversation with its newest messages (50 by default, at most 200)");

        group.MapGet("/{id:guid}/messages", (Guid id, DateTimeOffset? before, int? limit, ChatService chat, CancellationToken ct) => chat.GetMessagesAsync(id, before, limit, ct))
            .WithSummary("Page back through older messages: those written before 'before', oldest first");

        group.MapPost("/", async (StartConversationRequest request, ChatService chat, CancellationToken ct) =>
            {
                var conversation = await chat.StartAsync(request, ct);
                return TypedResults.Created($"/api/conversations/{conversation.Id}", conversation);
            })
            .WithSummary("Start a conversation with an orb");

        group.MapPut("/{id:guid}", (Guid id, RenameConversationRequest request, ChatService chat, CancellationToken ct) => chat.RenameAsync(id, request, ct))
            .WithSummary("Rename a conversation");

        group.MapDelete("/{id:guid}/messages/{messageId:guid}", async (Guid id, Guid messageId, ChatService chat, CancellationToken ct) =>
            {
                await chat.DeleteMessageAsync(id, messageId, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Delete one message from a conversation");

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
            .WithSummary("Send a message and stream the reply as server-sent events (start, delta, tool_call, tool_result, approval_required, done, error)");

        MapApprovals(group);
    }

    private static void MapApprovals(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/approvals/{approvalId:guid}", async (Guid id, Guid approvalId, DecideRequest request, ChatService chat, CancellationToken ct) =>
            {
                var events = await chat.DecideAsync(id, approvalId, request, ct);
                return TypedResults.ServerSentEvents(ToSse(events, ct));
            })
            .WithSummary("Answer a tool call that waits for approval (approve, deny or always) and stream the rest of the reply");

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
