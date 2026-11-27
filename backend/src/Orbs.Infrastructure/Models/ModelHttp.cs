using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Models;

/// <summary>Shared bits for HTTP model adapters.</summary>
internal static class ModelHttp
{
    public const string RestrictedHttpClientName = "models-restricted";

    /// <summary>The HTTP client for a provider call: admins may reach local models, everyone else goes through the network policy.</summary>
    public static HttpClient CreateClient(IHttpClientFactory factory, string clientName, ProviderConnection connection) =>
        factory.CreateClient(connection.RestrictNetwork ? RestrictedHttpClientName : clientName);

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
