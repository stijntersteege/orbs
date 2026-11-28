using Orbs.Application.Memories;

namespace Orbs.Api.Endpoints;

internal static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/orbs/{orbId:guid}/memories", (Guid orbId, MemoryService memories, CancellationToken ct) => memories.ListAsync(orbId, ct))
            .WithTags("Memory")
            .WithSummary("What an orb remembers, newest first");

        api.MapPost("/orbs/{orbId:guid}/memories", async (Guid orbId, SaveMemoryRequest request, MemoryService memories, CancellationToken ct) =>
            {
                var memory = await memories.AddAsync(orbId, request, cancellationToken: ct);
                return TypedResults.Created($"/api/memories/{memory.Id}", memory);
            })
            .WithTags("Memory")
            .WithSummary("Tell an orb something to remember");

        var group = api.MapGroup("/memories").WithTags("Memory");

        group.MapPut("/{id:guid}", (Guid id, SaveMemoryRequest request, MemoryService memories, CancellationToken ct) => memories.EditAsync(id, request, ct))
            .WithSummary("Change a memory");

        group.MapDelete("/{id:guid}", async (Guid id, MemoryService memories, CancellationToken ct) =>
            {
                await memories.ForgetAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Forget a memory");
    }
}
