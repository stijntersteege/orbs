using Orbs.Application.Tools;

namespace Orbs.Testing;

/// <summary>An MCP "server" in memory: one read-only tool that shouts its input, and one that acts.</summary>
public sealed class FakeMcpClient : IMcpClient
{
    public List<McpConnection> Connections { get; } = [];

    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(McpConnection connection, CancellationToken cancellationToken = default)
    {
        Connections.Add(connection);
        if (connection.Location.Contains("broken", StringComparison.Ordinal))
        {
            throw new HttpRequestException("connection refused");
        }

        return Task.FromResult<IReadOnlyList<McpToolInfo>>(
        [
            new McpToolInfo("shout", "Shout", "Repeats the text in capitals.", """{"type":"object","properties":{"text":{"type":"string"}}}""", ReadOnly: true),
            new McpToolInfo("turn_on", "Turn on", "Turns a light on.", """{"type":"object"}""", ReadOnly: false),
        ]);
    }

    public Task<string> CallToolAsync(McpConnection connection, string tool, string arguments, CancellationToken cancellationToken = default) =>
        Task.FromResult(tool == "shout" ? arguments.ToUpperInvariant() : "done");
}
