using Orbs.Application.Orbs;

namespace Orbs.Api.Endpoints;

internal static class OrbEndpoints
{
    public static void MapOrbEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/orbs").WithTags("Orbs");

        group.MapGet("/", (OrbService orbs, CancellationToken ct) => orbs.ListAsync(ct))
            .WithSummary("List all orbs");

        group.MapGet("/{id:guid}", (Guid id, OrbService orbs, CancellationToken ct) => orbs.GetAsync(id, ct))
            .WithSummary("Get one orb");

        group.MapPost("/", async (SaveOrbRequest request, OrbService orbs, CancellationToken ct) =>
            {
                var orb = await orbs.CreateAsync(request, ct);
                return TypedResults.Created($"/api/orbs/{orb.Id}", orb);
            })
            .WithSummary("Create an orb");

        group.MapPut("/{id:guid}", (Guid id, SaveOrbRequest request, OrbService orbs, CancellationToken ct) => orbs.UpdateAsync(id, request, ct))
            .WithSummary("Update an orb: name, brief, look, model and context");

        group.MapDelete("/{id:guid}", async (Guid id, OrbService orbs, CancellationToken ct) =>
            {
                await orbs.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Delete an orb and its conversations");
    }
}
