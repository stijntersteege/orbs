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
    }
}
