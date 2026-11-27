using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Orbs.Application.Tools;
using Orbs.Domain.Tools;

namespace Orbs.Infrastructure.Tools;

/// <summary>
/// The MCP client, on the official C# SDK. HTTP servers use the streamable HTTP transport (with SSE fallback);
/// stdio servers start a local process. Each operation opens a session and closes it again.
/// </summary>
internal sealed class McpClientAdapter(IHttpClientFactory httpClientFactory) : IMcpClient
{
    public const string HttpClientName = "mcp";
    public const string RestrictedHttpClientName = "mcp-restricted";

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(McpConnection connection, CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectAsync(connection, cancellationToken);
        var tools = new List<McpToolInfo>();
        string? cursor = null;
        do
        {
            var page = await client.ListToolsAsync(new ListToolsRequestParams { Cursor = cursor }, cancellationToken);
            tools.AddRange(page.Tools.Select(t => new McpToolInfo(
                t.Name,
                t.Title ?? t.Annotations?.Title,
                t.Description ?? string.Empty,
                t.InputSchema.ValueKind == JsonValueKind.Object ? t.InputSchema.GetRawText() : """{"type":"object"}""",
                t.Annotations?.ReadOnlyHint == true)));
            cursor = page.NextCursor;
        }
        while (!string.IsNullOrEmpty(cursor) && tools.Count < 500);

        return tools;
    }

    public async Task<string> CallToolAsync(McpConnection connection, string tool, string arguments, CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectAsync(connection, cancellationToken);
        var args = ParseArguments(arguments);
        var result = await client.CallToolAsync(new CallToolRequestParams { Name = tool, Arguments = args }, cancellationToken);
        var text = Flatten(result);
        if (result.IsError == true)
        {
            throw new InvalidOperationException(text.Length > 0 ? text : $"{tool} failed.");
        }

        return text;
    }

    internal static Dictionary<string, JsonElement> ParseArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(arguments);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Tool arguments must be a JSON object.");
        }

        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    /// <summary>Text blocks as they are, structured content as JSON, other blocks as a short note.</summary>
    internal static string Flatten(CallToolResult result)
    {
        var text = new StringBuilder();
        foreach (var block in result.Content)
        {
            if (text.Length > 0)
            {
                text.AppendLine();
            }

            text.Append(block is TextContentBlock t ? t.Text : $"[{block.Type} content]");
        }

        if (text.Length == 0 && result.StructuredContent is { } structured)
        {
            text.Append(structured.GetRawText());
        }

        return text.ToString();
    }

    private async Task<McpClient> ConnectAsync(McpConnection connection, CancellationToken cancellationToken)
    {
        IClientTransport transport = connection.Transport switch
        {
            McpTransport.Http => new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(connection.Location),
                    Name = "Orbs",
                    AdditionalHeaders = connection.Authorization is { Length: > 0 } auth
                        ? new Dictionary<string, string> { ["Authorization"] = auth }
                        : null,
                },
                httpClientFactory.CreateClient(connection.RestrictNetwork ? RestrictedHttpClientName : HttpClientName),
                NullLoggerFactory.Instance,
                ownsHttpClient: true),
            McpTransport.Stdio => new StdioClientTransport(StdioOptions(connection.Location), NullLoggerFactory.Instance),
            _ => throw new InvalidOperationException($"Unknown MCP transport {connection.Transport}."),
        };

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ClientInfo = new Implementation { Name = "Orbs", Version = "0.3.0" } },
            NullLoggerFactory.Instance,
            cancellationToken);
    }

    /// <summary>"npx -y @modelcontextprotocol/server-everything" becomes a command and its arguments.</summary>
    internal static StdioClientTransportOptions StdioOptions(string commandLine)
    {
        var parts = Split(commandLine);
        if (parts.Count == 0)
        {
            throw new ArgumentException("The command is empty.");
        }

        return new StdioClientTransportOptions { Name = "Orbs", Command = parts[0], Arguments = [.. parts.Skip(1)] };
    }

    /// <summary>Splits on spaces, keeping "quoted parts" together.</summary>
    internal static List<string> Split(string commandLine)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        foreach (var c in commandLine)
        {
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }
}
