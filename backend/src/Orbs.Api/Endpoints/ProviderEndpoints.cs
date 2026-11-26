using Orbs.Application.Providers;

namespace Orbs.Api.Endpoints;

internal static class ProviderEndpoints
{
    public static void MapProviderEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/providers").WithTags("Providers");

        group.MapGet("/kinds", (ProviderService providers) => providers.ListKinds())
            .WithSummary("List the kinds of provider this server can talk to");

        group.MapGet("/", (ProviderService providers, CancellationToken ct) => providers.ListAsync(ct))
            .WithSummary("List connected providers");

        group.MapGet("/{id:guid}", (Guid id, ProviderService providers, CancellationToken ct) => providers.GetAsync(id, ct))
            .WithSummary("Get one provider");

        group.MapGet("/{id:guid}/models", (Guid id, ProviderService providers, CancellationToken ct) => providers.ListModelsAsync(id, ct))
            .WithSummary("List the models a provider offers");

        group.MapPost("/", async (SaveProviderRequest request, ProviderService providers, CancellationToken ct) =>
            {
                var provider = await providers.CreateAsync(request, ct);
                return TypedResults.Created($"/api/providers/{provider.Id}", provider);
            })
            .WithSummary("Connect a provider");

        group.MapPut("/{id:guid}", (Guid id, SaveProviderRequest request, ProviderService providers, CancellationToken ct) => providers.UpdateAsync(id, request, ct))
            .WithSummary("Update a provider. A null apiKey keeps the stored key, an empty one removes it");

        group.MapDelete("/{id:guid}", async (Guid id, ProviderService providers, CancellationToken ct) =>
            {
                await providers.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Disconnect a provider");
    }
}
