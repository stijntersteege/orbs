using System.Net.Http.Headers;
using System.Text.Json;
using Orbs.Application.Abstractions;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Integrations;

/// <summary>A tool defined inline by an integration: metadata plus a function.</summary>
internal sealed class DelegateTool(
    string name,
    string displayName,
    string description,
    string schema,
    ToolRisk risk,
    Func<JsonElement, CancellationToken, Task<string>> invoke) : ITool
{
    public string Kind => name;

    public string DisplayName => displayName;

    public string Description => description;

    public string ParametersSchema => schema;

    public ToolRisk Risk => risk;

    public async Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(invocation.Arguments) ? "{}" : invocation.Arguments);
        return await invoke(doc.RootElement.Clone(), cancellationToken);
    }
}

/// <summary>Small helpers shared by the integrations.</summary>
internal static class IntegrationKit
{
    public const string HttpClientName = "integrations";
    public const string RestrictedHttpClientName = "integrations-restricted";

    public static HttpClient Http(IHttpClientFactory factory, IntegrationConfig config) =>
        factory.CreateClient(config.RestrictNetwork ? RestrictedHttpClientName : HttpClientName);

    public static string? Text(this JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static string Required(this JsonElement args, string name) =>
        args.Text(name) is { Length: > 0 } value ? value : throw new ArgumentException($"'{name}' is required.");

    public static int Int(this JsonElement args, string name, int fallback, int min, int max) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
            ? Math.Clamp(number, min, max)
            : fallback;

    public static bool Bool(this JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    public static Uri BaseUri(IntegrationConfig config, string key)
    {
        var raw = config.Get(key);
        return Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : throw new InvalidOperationException($"{raw} is not an http(s) address.");
    }

    public static void Bearer(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public static async Task<string> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {Clip(body, 300)}", null, response.StatusCode);
        }

        return body;
    }

    /// <summary>For non-admins, refuses mail servers and other raw connections on private networks.</summary>
    public static async Task EnsureReachableAsync(IntegrationConfig config, string host, CancellationToken cancellationToken)
    {
        if (!config.RestrictNetwork)
        {
            return;
        }

        var addresses = System.Net.IPAddress.TryParse(host, out var literal) ? [literal] : await System.Net.Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(Security.SafeHttp.IsInternal))
        {
            throw new Security.BlockedAddressException($"Orbs does not connect to addresses on private networks ({host}). Ask an admin to set this up.");
        }
    }

    public static string Clip(string text, int max) => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
