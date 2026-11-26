using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Models;

/// <summary>
/// Talks to any endpoint that speaks the OpenAI Chat Completions API:
/// OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM, Groq, Mistral and many more.
/// </summary>
internal sealed class OpenAiCompatibleModelAdapter(IHttpClientFactory httpClientFactory) : IModelAdapter
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

    public async IAsyncEnumerable<string> StreamChatAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        var body = new CompletionRequest(
            request.Model,
            [.. request.Messages.Select(m => new CompletionMessage(m.Role.ToString().ToLowerInvariant(), m.Content))],
            Stream: true);

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint(connection, "chat/completions"))
        {
            Content = JsonContent.Create(body, options: Json),
        };
        Authorize(message, connection);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]")
            {
                yield break;
            }

            var text = ParseDelta(data);
            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
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

    private sealed record CompletionRequest(string Model, IReadOnlyList<CompletionMessage> Messages, bool Stream);

    private sealed record CompletionMessage(string Role, string Content);

    private sealed record ModelList(IReadOnlyList<ModelEntry>? Data);

    private sealed record ModelEntry(string Id, string? Name);
}
