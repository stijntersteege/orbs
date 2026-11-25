using Orbs.Application.Context;

namespace Orbs.Api.Endpoints;

internal static class ContextEndpoints
{
    public static void MapContextEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/context-sources").WithTags("Context");

        group.MapGet("/kinds", (ContextService context) => context.ListKinds())
            .WithSummary("List the kinds of context this server can read");

        group.MapGet("/", (ContextService context, CancellationToken ct) => context.ListAsync(ct))
            .WithSummary("List context sources");

        group.MapGet("/{id:guid}", (Guid id, ContextService context, CancellationToken ct) => context.GetAsync(id, ct))
            .WithSummary("Get one context source");

        group.MapPost("/", async (SaveContextSourceRequest request, ContextService context, CancellationToken ct) =>
            {
                var source = await context.CreateAsync(request, ct);
                return TypedResults.Created($"/api/context-sources/{source.Id}", source);
            })
            .WithSummary("Add a context source and read it");

        group.MapPut("/{id:guid}", (Guid id, SaveContextSourceRequest request, ContextService context, CancellationToken ct) => context.UpdateAsync(id, request, ct))
            .WithSummary("Update a context source and read it again");

        group.MapPost("/{id:guid}/sync", (Guid id, ContextService context, CancellationToken ct) => context.SyncAsync(id, ct))
            .WithSummary("Read a context source again");

        group.MapDelete("/{id:guid}", async (Guid id, ContextService context, CancellationToken ct) =>
            {
                await context.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Remove a context source from every orb and delete it");
    }
}
