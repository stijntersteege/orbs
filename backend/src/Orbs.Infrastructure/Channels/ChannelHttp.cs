using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Channels;
using Orbs.Infrastructure.Integrations;

namespace Orbs.Infrastructure.Channels;

/// <summary>HTTP helpers shared by the channel adapters.</summary>
internal static class ChannelHttp
{
    /// <summary>Like integrations: admins may reach their own network, everyone else goes through the network policy.</summary>
    public static HttpClient Client(IHttpClientFactory factory, ChannelConfig config) =>
        factory.CreateClient(config.RestrictNetwork ? IntegrationKit.RestrictedHttpClientName : IntegrationKit.HttpClientName);

    public static async Task<JsonDocument> SendAsync(HttpClient client, HttpMethod method, Uri uri, string? bearer, JsonNode? body, CancellationToken cancellationToken, string scheme = "Bearer")
    {
        using var request = new HttpRequestMessage(method, uri);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, bearer);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        var text = await IntegrationKit.SendAsync(client, request, cancellationToken);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
    }

    public static Uri Base(ChannelConfig config, string key)
    {
        var raw = config.Get(key);
        return Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : throw new InvalidOperationException($"{raw} is not an http(s) address.");
    }

    public static string? Text(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
