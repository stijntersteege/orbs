using Orbs.Application.Tools;

namespace Orbs.Api.Endpoints;

internal static class McpEndpoints
{
    public static void MapMcpEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/mcp-servers").WithTags("MCP servers");

        group.MapGet("/", (McpServerService mcp, CancellationToken ct) => mcp.ListAsync(ct))
            .WithSummary("Your connected MCP servers");

        group.MapPost("/", async (SaveMcpServerRequest request, McpServerService mcp, CancellationToken ct) =>
            {
                var server = await mcp.CreateAsync(request, ct);
                return TypedResults.Created($"/api/mcp-servers/{server.Id}", server);
            })
            .WithSummary("Connect an MCP server (HTTP, or a local command when allowed)");

        group.MapPut("/{id:guid}", (Guid id, SaveMcpServerRequest request, McpServerService mcp, CancellationToken ct) => mcp.UpdateAsync(id, request, ct))
            .WithSummary("Change an MCP server");

        group.MapDelete("/{id:guid}", async (Guid id, McpServerService mcp, CancellationToken ct) =>
            {
                await mcp.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Disconnect an MCP server");

        group.MapGet("/{id:guid}/tools", (Guid id, McpServerService mcp, CancellationToken ct) => mcp.ToolsAsync(id, ct))
            .WithSummary("Connect now and list the server's tools (or the error)");

        var integrations = api.MapGroup("/integrations").WithTags("Integrations");

        integrations.MapGet("/kinds", (IntegrationService service) => service.Kinds())
            .WithSummary("Apps Orbs can connect to, and what each needs");

        integrations.MapGet("/", (IntegrationService service, CancellationToken ct) => service.ListAsync(ct))
            .WithSummary("Your connected apps and the tools they bring");

        integrations.MapPost("/", async (SaveIntegrationRequest request, IntegrationService service, CancellationToken ct) =>
            {
                var integration = await service.CreateAsync(request, ct);
                return TypedResults.Created($"/api/integrations/{integration.Id}", integration);
            })
            .WithSummary("Connect an app: web search, calendar, email or Home Assistant");

        integrations.MapPut("/{id:guid}", (Guid id, SaveIntegrationRequest request, IntegrationService service, CancellationToken ct) => service.UpdateAsync(id, request, ct))
            .WithSummary("Change a connected app");

        integrations.MapDelete("/{id:guid}", async (Guid id, IntegrationService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Disconnect an app");

        integrations.MapPost("/{id:guid}/test", (Guid id, IntegrationService service, CancellationToken ct) => service.TestAsync(id, ct))
            .WithSummary("Check that the settings work");
    }
}
