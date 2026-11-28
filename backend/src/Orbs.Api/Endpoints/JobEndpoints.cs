using Orbs.Application.Jobs;
using Orbs.Application.Tools;

namespace Orbs.Api.Endpoints;

internal static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGroup("/jobs").WithTags("Background jobs")
            .MapGet("/", (JobScheduler queue, CancellationToken ct) => queue.ListRecentAsync(ct))
            .WithSummary("Your most recent background jobs: what ran, when, and how it went");

        api.MapGroup("/tools").WithTags("Tools")
            .MapGet("/", (ToolCatalog catalog, CancellationToken ct) => catalog.DescribeAsync(ct))
            .WithSummary("Every tool your orbs can use: built in, from MCP servers and from integrations");
    }
}
