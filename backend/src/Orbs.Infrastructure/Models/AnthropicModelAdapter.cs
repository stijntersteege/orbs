using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Models;

/// <summary>
/// Talks to the Anthropic Messages API natively: system prompt apart from the messages,
/// tool_use and tool_result content blocks, and server-sent events for streaming.
/// </summary>
internal sealed class AnthropicModelAdapter(IHttpClientFactory httpClientFactory) : IModelAdapter
{
    public const string HttpClientName = "anthropic";
    public const string ApiVersion = "2023-06-01";
    private const int MaxTokens = 8_192;

    public string Kind => "anthropic";

    public string DisplayName => "Anthropic";

    public string Description => "Claude models through the Anthropic Messages API, with tool use.";

    public bool RequiresBaseUrl => false;

    public bool RequiresApiKey => true;

    public Uri? DefaultBaseUrl { get; } = new("https://api.anthropic.com");

    public bool SupportsTools => true;

    public bool SupportsImages => true;

    public async IAsyncEnumerable<string> StreamChatAsync(ProviderConnection connection, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var output in StreamAsync(connection, request, cancellationToken))
        {
            if (output is TextOutput text)
            {
                yield return text.Text;
            }
        }
    }

    public async IAsyncEnumerable<ModelOutput> StreamAsync(ProviderConnection connection, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);
        using var message = Request(connection, HttpMethod.Post, "v1/messages");
        message.Content = new StringContent(BuildRequest(request).ToJsonString(), Encoding.UTF8, "application/json");

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ModelHttp.EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var parser = new StreamParser();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var output in parser.Read(line["data:".Length..].Trim()))
            {
                yield return output;
            }
        }
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var message = Request(connection, HttpMethod.Get, "v1/models?limit=100");
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, cancellationToken);
        await ModelHttp.EnsureSuccessAsync(response, cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return [.. doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(m => new ModelInfo(m.GetProperty("id").GetString()!, m.TryGetProperty("display_name", out var n) ? n.GetString() : null))
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The Messages API body. System turns go to <c>system</c>; tool results become <c>tool_result</c> blocks in a
    /// user message; consecutive turns of the same role are merged, as the API requires alternating roles.
    /// </summary>
    internal static JsonObject BuildRequest(ChatRequest request)
    {
        var system = string.Join("\n\n", request.Messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));
        var messages = new JsonArray();
        string? lastRole = null;
        JsonArray? lastContent = null;
        foreach (var turn in request.Messages.Where(m => m.Role != ChatRole.System))
        {
            var role = turn.Role == ChatRole.Assistant ? "assistant" : "user";
            var blocks = new JsonArray();
            if (turn.Role == ChatRole.Tool)
            {
                blocks.Add(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = turn.ToolCallId, ["content"] = turn.Content });
            }
            else
            {
                if (turn.Content.Length > 0)
                {
                    blocks.Add(new JsonObject { ["type"] = "text", ["text"] = turn.Content });
                }

                foreach (var image in turn.Images ?? [])
                {
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "image",
                        ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = image.MediaType, ["data"] = image.Base64 },
                    });
                }

                foreach (var call in turn.ToolCalls ?? [])
                {
                    blocks.Add(new JsonObject { ["type"] = "tool_use", ["id"] = call.Id, ["name"] = call.Name, ["input"] = ModelHttp.ParseObject(call.Arguments) });
                }
            }

            if (blocks.Count == 0)
            {
                continue;
            }

            if (role == lastRole && lastContent is not null)
            {
                foreach (var block in blocks.ToList())
                {
                    blocks.Remove(block);
                    lastContent.Add(block);
                }
            }
            else
            {
                messages.Add(new JsonObject { ["role"] = role, ["content"] = blocks });
                lastRole = role;
                lastContent = blocks;
            }
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = MaxTokens,
            ["stream"] = true,
            ["messages"] = messages,
        };
        if (system.Length > 0)
        {
            body["system"] = system;
        }

        if (request.Tools is { Count: > 0 } tools)
        {
            body["tools"] = new JsonArray([.. tools.Select(t => (JsonNode)new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["input_schema"] = JsonNode.Parse(t.ParametersSchema),
            })]);
        }

        return body;
    }

    private static HttpRequestMessage Request(ProviderConnection connection, HttpMethod method, string path)
    {
        var baseUrl = (connection.BaseUrl ?? new Uri("https://api.anthropic.com")).ToString().TrimEnd('/') + "/";
        var message = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), path));
        message.Headers.Add("x-api-key", connection.ApiKey ?? string.Empty);
        message.Headers.Add("anthropic-version", ApiVersion);
        return message;
    }

    /// <summary>Turns streamed events into text and complete tool calls (whose input arrives as JSON pieces).</summary>
    internal sealed class StreamParser
    {
        private readonly Dictionary<int, (string Id, string Name, StringBuilder Input)> _tools = [];

        public IEnumerable<ModelOutput> Read(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            var index = root.TryGetProperty("index", out var i) && i.TryGetInt32(out var n) ? n : 0;
            switch (type)
            {
                case "content_block_start" when root.GetProperty("content_block") is var block && block.GetProperty("type").GetString() == "tool_use":
                    _tools[index] = (block.GetProperty("id").GetString()!, block.GetProperty("name").GetString()!, new StringBuilder());
                    break;
                case "content_block_delta":
                    var delta = root.GetProperty("delta");
                    var kind = delta.GetProperty("type").GetString();
                    if (kind == "text_delta")
                    {
                        return [new TextOutput(delta.GetProperty("text").GetString() ?? string.Empty)];
                    }

                    if (kind == "input_json_delta" && _tools.TryGetValue(index, out var pending))
                    {
                        pending.Input.Append(delta.GetProperty("partial_json").GetString());
                    }

                    break;
                case "content_block_stop" when _tools.Remove(index, out var done):
                    return [new ToolCallOutput(new ToolCall(done.Id, done.Name, done.Input.Length == 0 ? "{}" : done.Input.ToString()))];
                case "error":
                    throw new HttpRequestException(root.GetProperty("error").GetProperty("message").GetString());
            }

            return [];
        }
    }
}

/// <summary>Shared bits for HTTP model adapters.</summary>
internal static class ModelHttp
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 300)
        {
            detail = detail[..300];
        }

        throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {detail}", null, response.StatusCode);
    }

    /// <summary>Tool arguments as a JSON object; anything else becomes an empty object.</summary>
    public static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
