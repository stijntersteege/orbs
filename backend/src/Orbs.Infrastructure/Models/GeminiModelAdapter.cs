using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Models;

/// <summary>
/// Talks to the Google Gemini API natively: <c>systemInstruction</c>, <c>contents</c> with user and model roles,
/// <c>functionDeclarations</c> for tools, and <c>streamGenerateContent</c> over server-sent events.
/// </summary>
internal sealed class GeminiModelAdapter(IHttpClientFactory httpClientFactory) : IModelAdapter, IEmbeddingAdapter
{
    public const string HttpClientName = "gemini";

    /// <summary>JSON Schema keywords Gemini's OpenAPI subset rejects. They are dropped from tool schemas.</summary>
    private static readonly HashSet<string> UnsupportedSchemaKeys = ["$schema", "$id", "$ref", "$defs", "definitions", "additionalProperties", "examples", "default", "const", "title"];

    public string Kind => "gemini";

    public string DisplayName => "Google Gemini";

    public string Description => "Gemini models through the Google AI (Gemini) API, with function calling.";

    public bool RequiresBaseUrl => false;

    public bool RequiresApiKey => true;

    public Uri? DefaultBaseUrl { get; } = new("https://generativelanguage.googleapis.com");

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
        var model = request.Model.StartsWith("models/", StringComparison.Ordinal) ? request.Model : "models/" + request.Model;
        using var message = Request(connection, HttpMethod.Post, $"v1beta/{model}:streamGenerateContent?alt=sse");
        message.Content = new StringContent(BuildRequest(request).ToJsonString(), Encoding.UTF8, "application/json");

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ModelHttp.EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var calls = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var output in ParseChunk(line["data:".Length..].Trim(), ref calls))
            {
                yield return output;
            }
        }
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var message = Request(connection, HttpMethod.Get, "v1beta/models?pageSize=200");
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, cancellationToken);
        await ModelHttp.EnsureSuccessAsync(response, cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return [.. doc.RootElement.GetProperty("models").EnumerateArray()
            .Where(m => m.TryGetProperty("supportedGenerationMethods", out var methods) && methods.EnumerateArray().Any(x => x.GetString() == "generateContent"))
            .Select(m => new ModelInfo(m.GetProperty("name").GetString()!.Replace("models/", string.Empty, StringComparison.Ordinal), m.TryGetProperty("displayName", out var d) ? d.GetString() : null))
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>batchEmbedContents: one request for many texts.</summary>
    public async Task<IReadOnlyList<float[]>> EmbedAsync(ProviderConnection connection, string model, IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(texts);
        var name = model.StartsWith("models/", StringComparison.Ordinal) ? model : "models/" + model;
        var body = new JsonObject
        {
            ["requests"] = new JsonArray([.. texts.Select(t => (JsonNode)new JsonObject
            {
                ["model"] = name,
                ["content"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = t }) },
            })]),
        };
        using var message = Request(connection, HttpMethod.Post, $"v1beta/{name}:batchEmbedContents");
        message.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, cancellationToken);
        await ModelHttp.EnsureSuccessAsync(response, cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return [.. doc.RootElement.GetProperty("embeddings").EnumerateArray()
            .Select(e => e.GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray())];
    }

    /// <summary>The generateContent body. Tool results become <c>functionResponse</c> parts named after their call.</summary>
    internal static JsonObject BuildRequest(ChatRequest request)
    {
        var names = request.Messages.SelectMany(m => m.ToolCalls ?? []).ToDictionary(c => c.Id, c => c.Name, StringComparer.Ordinal);
        var system = string.Join("\n\n", request.Messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));
        var contents = new JsonArray();
        string? lastRole = null;
        JsonArray? lastParts = null;
        foreach (var turn in request.Messages.Where(m => m.Role != ChatRole.System))
        {
            var role = turn.Role == ChatRole.Assistant ? "model" : "user";
            var parts = new JsonArray();
            if (turn.Role == ChatRole.Tool)
            {
                parts.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = turn.ToolCallId is not null && names.TryGetValue(turn.ToolCallId, out var name) ? name : "tool",
                        ["response"] = new JsonObject { ["content"] = turn.Content },
                    },
                });
            }
            else
            {
                if (turn.Content.Length > 0)
                {
                    parts.Add(new JsonObject { ["text"] = turn.Content });
                }

                foreach (var image in turn.Images ?? [])
                {
                    parts.Add(new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = image.MediaType, ["data"] = image.Base64 } });
                }

                foreach (var call in turn.ToolCalls ?? [])
                {
                    parts.Add(new JsonObject { ["functionCall"] = new JsonObject { ["name"] = call.Name, ["args"] = ModelHttp.ParseObject(call.Arguments) } });
                }
            }

            if (parts.Count == 0)
            {
                continue;
            }

            if (role == lastRole && lastParts is not null)
            {
                foreach (var part in parts.ToList())
                {
                    parts.Remove(part);
                    lastParts.Add(part);
                }
            }
            else
            {
                contents.Add(new JsonObject { ["role"] = role, ["parts"] = parts });
                lastRole = role;
                lastParts = parts;
            }
        }

        var body = new JsonObject { ["contents"] = contents };
        if (system.Length > 0)
        {
            body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) };
        }

        if (request.Tools is { Count: > 0 } tools)
        {
            body["tools"] = new JsonArray(new JsonObject
            {
                ["functionDeclarations"] = new JsonArray([.. tools.Select(t => (JsonNode)new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = CleanSchema(JsonNode.Parse(t.ParametersSchema)),
                })]),
            });
        }

        return body;
    }

    /// <summary>Removes schema keywords Gemini does not accept, at every level.</summary>
    internal static JsonNode? CleanSchema(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(p => p.Key).ToList())
            {
                if (UnsupportedSchemaKeys.Contains(key))
                {
                    obj.Remove(key);
                }
                else if (key == "properties" && obj[key] is JsonObject properties)
                {
                    // Property names are data, not keywords: keep them, clean their schemas.
                    foreach (var property in properties)
                    {
                        CleanSchema(property.Value);
                    }
                }
                else
                {
                    CleanSchema(obj[key]);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                CleanSchema(item);
            }
        }

        return node;
    }

    /// <summary>One streamed chunk: text parts and function calls (which Gemini sends whole).</summary>
    internal static IReadOnlyList<ModelOutput> ParseChunk(string json, ref int callCount)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            throw new HttpRequestException(error.TryGetProperty("message", out var m) ? m.GetString() : "Gemini returned an error.");
        }

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0
            || !candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
        {
            return [];
        }

        var outputs = new List<ModelOutput>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text) && text.GetString() is { Length: > 0 } value
                && !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
            {
                outputs.Add(new TextOutput(value));
            }
            else if (part.TryGetProperty("functionCall", out var call))
            {
                var id = call.TryGetProperty("id", out var callId) && callId.GetString() is { Length: > 0 } given ? given : $"call_{++callCount}";
                var args = call.TryGetProperty("args", out var a) ? a.GetRawText() : "{}";
                outputs.Add(new ToolCallOutput(new ToolCall(id, call.GetProperty("name").GetString()!, args)));
            }
        }

        return outputs;
    }

    private static HttpRequestMessage Request(ProviderConnection connection, HttpMethod method, string path)
    {
        var baseUrl = (connection.BaseUrl ?? new Uri("https://generativelanguage.googleapis.com")).ToString().TrimEnd('/') + "/";
        var message = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), path));
        message.Headers.Add("x-goog-api-key", connection.ApiKey ?? string.Empty);
        return message;
    }
}
