using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Orbs.Application.Abstractions;
using Orbs.Application.Voice;

namespace Orbs.Infrastructure.Models;

/// <summary>
/// Talks to any endpoint that speaks the OpenAI Chat Completions API:
/// OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM, Groq, Mistral and many more.
/// Supports function calling: tools go out as <c>tools</c>, calls come back as streamed <c>tool_calls</c>.
/// </summary>
internal sealed class OpenAiCompatibleModelAdapter(IHttpClientFactory httpClientFactory) : IModelAdapter, IEmbeddingAdapter, ISpeechToTextAdapter, ITextToSpeechAdapter
{
    public const string HttpClientName = "openai-compatible";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Kind => "openai-compatible";

    public string DisplayName => "OpenAI-compatible";

    public string Description => "Any endpoint that speaks the OpenAI chat API: OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM.";

    public bool RequiresBaseUrl => true;

    public bool RequiresApiKey => false;

    public Uri? DefaultBaseUrl => null;

    public bool SupportsTools => true;

    public bool SupportsImages => true;

    public async IAsyncEnumerable<string> StreamChatAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var output in StreamAsync(connection, request, cancellationToken))
        {
            if (output is TextOutput text)
            {
                yield return text.Text;
            }
        }
    }

    public async IAsyncEnumerable<ModelOutput> StreamAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint(connection, "chat/completions"))
        {
            Content = new StringContent(BuildRequest(request).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        Authorize(message, connection);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var calls = new ToolCallAccumulator();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]")
            {
                break;
            }

            var text = ParseDelta(data);
            if (!string.IsNullOrEmpty(text))
            {
                yield return new TextOutput(text);
            }

            calls.Add(data);
        }

        foreach (var call in calls.Complete())
        {
            yield return new ToolCallOutput(call);
        }
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var message = new HttpRequestMessage(HttpMethod.Get, Endpoint(connection, "models"));
        Authorize(message, connection);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var list = await response.Content.ReadFromJsonAsync<ModelList>(Json, cancellationToken);
        return [.. (list?.Data ?? []).Select(m => new ModelInfo(m.Id, m.Name)).OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)];
    }

    public IReadOnlyList<string> SuggestedVoices { get; } = ["alloy", "ash", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer"];

    /// <summary>POST /audio/transcriptions with the recording as a file: OpenAI Whisper, faster-whisper servers, LocalAI.</summary>
    public async Task<string> TranscribeAsync(ProviderConnection connection, string model, byte[] audio, string contentType, string? language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audio);
        // The type and name come from a fixed list, never straight from the upload.
        var extension = AudioExtension(contentType);
        file.Headers.ContentType = new MediaTypeHeaderValue(AudioMediaType(extension));
        form.Add(file, "file", "speech" + extension);
        form.Add(new StringContent(model), "model");
        if (!string.IsNullOrWhiteSpace(language))
        {
            form.Add(new StringContent(language), "language");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint(connection, "audio/transcriptions")) { Content = form };
        Authorize(message, connection);
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return doc.RootElement.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>POST /audio/speech: OpenAI TTS, Kokoro-FastAPI, openedai-speech (Piper) and friends. Answers MP3.</summary>
    public async Task<SpokenAudio> SpeakAsync(ProviderConnection connection, string model, string voice, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var body = new JsonObject { ["model"] = model, ["voice"] = voice, ["input"] = text, ["response_format"] = "mp3" };
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint(connection, "audio/speech"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        Authorize(message, connection);
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return new SpokenAudio(await response.Content.ReadAsByteArrayAsync(cancellationToken), response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg");
    }

    internal static string AudioMediaType(string extension) => extension switch
    {
        ".ogg" => "audio/ogg",
        ".m4a" => "audio/mp4",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        _ => "audio/webm",
    };

    internal static string AudioExtension(string contentType) => contentType.Split(';')[0].Trim() switch
    {
        "audio/webm" => ".webm",
        "audio/ogg" => ".ogg",
        "audio/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
        "audio/mpeg" => ".mp3",
        "audio/wav" or "audio/x-wav" => ".wav",
        _ => ".webm",
    };

    /// <summary>POST /embeddings: one vector per input, put back in input order.</summary>
    public async Task<IReadOnlyList<float[]>> EmbedAsync(ProviderConnection connection, string model, IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(texts);
        var body = new JsonObject { ["model"] = model, ["input"] = new JsonArray([.. texts.Select(t => (JsonNode)JsonValue.Create(t)!)]) };
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint(connection, "embeddings"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        Authorize(message, connection);
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return ParseEmbeddings(await response.Content.ReadAsStringAsync(cancellationToken), texts.Count);
    }

    internal static IReadOnlyList<float[]> ParseEmbeddings(string json, int expected)
    {
        using var doc = JsonDocument.Parse(json);
        var vectors = new float[expected][];
        var position = 0;
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var index = item.TryGetProperty("index", out var i) ? i.GetInt32() : position;
            vectors[index] = [.. item.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle())];
            position++;
        }

        return vectors.Any(v => v is null) ? throw new HttpRequestException("The provider returned fewer embeddings than asked for.") : vectors;
    }

    /// <summary>The Chat Completions request body, including tool definitions and earlier tool turns.</summary>
    internal static JsonObject BuildRequest(ChatRequest request)
    {
        var messages = new JsonArray();
        foreach (var turn in request.Messages)
        {
            var item = new JsonObject
            {
                ["role"] = turn.Role.ToString().ToLowerInvariant(),
                ["content"] = turn.Images is { Count: > 0 } images
                    ? new JsonArray([
                        new JsonObject { ["type"] = "text", ["text"] = turn.Content },
                        .. images.Select(i => (JsonNode)new JsonObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JsonObject { ["url"] = $"data:{i.MediaType};base64,{i.Base64}" },
                        })])
                    : turn.Content,
            };
            if (turn.ToolCalls is { Count: > 0 } calls)
            {
                item["tool_calls"] = new JsonArray([.. calls.Select(c => (JsonNode)new JsonObject
                {
                    ["id"] = c.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments },
                })]);
            }

            if (turn.ToolCallId is not null)
            {
                item["tool_call_id"] = turn.ToolCallId;
            }

            messages.Add(item);
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = true,
        };
        if (request.Tools is { Count: > 0 } tools)
        {
            body["tools"] = new JsonArray([.. tools.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = JsonNode.Parse(t.ParametersSchema),
                },
            })]);
        }

        return body;
    }

    internal static string? ParseDelta(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return null;
        }

        return choices[0].TryGetProperty("delta", out var delta)
               && delta.TryGetProperty("content", out var content)
               && content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : null;
    }

    internal static Uri Endpoint(ProviderConnection connection, string path)
    {
        var baseUrl = connection.BaseUrl ?? throw new InvalidOperationException("This provider has no base URL.");
        var root = baseUrl.ToString().TrimEnd('/') + "/";
        return new Uri(new Uri(root), path);
    }

    private static void Authorize(HttpRequestMessage message, ProviderConnection connection)
    {
        if (!string.IsNullOrEmpty(connection.ApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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

    private sealed record ModelList(IReadOnlyList<ModelEntry>? Data);

    private sealed record ModelEntry(string Id, string? Name);
}

/// <summary>
/// Streamed tool calls arrive in pieces: the first chunk names the call, later chunks add bits of the
/// arguments. Pieces are keyed by their index and joined when the stream ends.
/// </summary>
internal sealed class ToolCallAccumulator
{
    private readonly SortedDictionary<int, Pending> _calls = [];

    public void Add(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("delta", out var delta)
            || !delta.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var piece in toolCalls.EnumerateArray())
        {
            var index = piece.TryGetProperty("index", out var i) && i.TryGetInt32(out var n) ? n : _calls.Count;
            if (!_calls.TryGetValue(index, out var current))
            {
                current = new Pending();
                _calls[index] = current;
            }

            if (piece.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                current.Id = id.GetString();
            }

            if (piece.TryGetProperty("function", out var function))
            {
                if (function.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    current.Name += name.GetString();
                }

                if (function.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String)
                {
                    current.Arguments.Append(args.GetString());
                }
            }
        }
    }

    public IReadOnlyList<ToolCall> Complete() =>
        [.. _calls.Where(c => c.Value.Name.Length > 0)
            .Select(c => new ToolCall(
                c.Value.Id ?? $"call_{c.Key}",
                c.Value.Name,
                c.Value.Arguments.Length == 0 ? "{}" : c.Value.Arguments.ToString()))];

    private sealed class Pending
    {
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public StringBuilder Arguments { get; } = new();
    }
}
