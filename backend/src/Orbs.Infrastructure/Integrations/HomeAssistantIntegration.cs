using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Abstractions;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Integrations;

/// <summary>Home Assistant through its REST API: read entity states and call services.</summary>
internal sealed class HomeAssistantIntegration(IHttpClientFactory httpClientFactory) : IIntegrationKind
{
    public string Kind => "home-assistant";

    public string DisplayName => "Home Assistant";

    public string Description => "See and control your smart home: lights, sensors, climate and more.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("url", "Home Assistant address", Required: true, Placeholder: "http://homeassistant.local:8123"),
        new("token", "Long-lived access token", Secret: true, Hint: "Create one in your Home Assistant profile, under Security."),
    ];

    public IReadOnlyList<ITool> CreateTools(IntegrationConfig config) =>
    [
        new DelegateTool(
            "home_list_entities",
            "List smart home devices",
            "List Home Assistant entities with their state, optionally for one domain such as light, switch, sensor or climate.",
            """{"type":"object","properties":{"domain":{"type":"string","description":"For example light or sensor."}}}""",
            ToolRisk.Read,
            (args, ct) => ListAsync(config, args.Text("domain"), ct)),
        new DelegateTool(
            "home_call_service",
            "Control a smart home device",
            "Call a Home Assistant service, for example domain light, service turn_on, entityId light.kitchen.",
            """{"type":"object","properties":{"domain":{"type":"string"},"service":{"type":"string"},"entityId":{"type":"string"},"data":{"type":"object","description":"Extra service data, such as brightness_pct."}},"required":["domain","service"]}""",
            ToolRisk.Write,
            (args, ct) => CallAsync(config, args, ct)),
    ];

    public async Task TestAsync(IntegrationConfig config, CancellationToken cancellationToken = default)
    {
        using var request = Request(config, HttpMethod.Get, "api/");
        _ = await IntegrationKit.SendAsync(IntegrationKit.Http(httpClientFactory, config), request, cancellationToken);
    }

    internal async Task<string> ListAsync(IntegrationConfig config, string? domain, CancellationToken cancellationToken)
    {
        using var request = Request(config, HttpMethod.Get, "api/states");
        using var doc = JsonDocument.Parse(await IntegrationKit.SendAsync(IntegrationKit.Http(httpClientFactory, config), request, cancellationToken));
        var text = new StringBuilder();
        var count = 0;
        foreach (var entity in doc.RootElement.EnumerateArray())
        {
            var id = entity.Text("entity_id") ?? string.Empty;
            if (domain is { Length: > 0 } && !id.StartsWith(domain + ".", StringComparison.Ordinal))
            {
                continue;
            }

            var name = entity.TryGetProperty("attributes", out var attributes) ? attributes.Text("friendly_name") : null;
            text.Append(id).Append(": ").Append(entity.Text("state"));
            if (name is not null)
            {
                text.Append(" (").Append(name).Append(')');
            }

            text.AppendLine();
            if (++count == 200)
            {
                text.AppendLine("... more entities left out. Ask for one domain.");
                break;
            }
        }

        return count == 0 ? "No entities found." : text.ToString().TrimEnd();
    }

    internal async Task<string> CallAsync(IntegrationConfig config, JsonElement args, CancellationToken cancellationToken)
    {
        var domain = Safe(args.Required("domain"));
        var service = Safe(args.Required("service"));
        var data = args.TryGetProperty("data", out var extra) && extra.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(extra.GetRawText())!.AsObject()
            : [];
        if (args.Text("entityId") is { Length: > 0 } entity)
        {
            data["entity_id"] = entity;
        }

        using var request = Request(config, HttpMethod.Post, $"api/services/{domain}/{service}");
        request.Content = new StringContent(data.ToJsonString(), Encoding.UTF8, "application/json");
        _ = await IntegrationKit.SendAsync(IntegrationKit.Http(httpClientFactory, config), request, cancellationToken);
        return $"Called {domain}.{service}" + (data["entity_id"] is { } target ? $" on {target}." : ".");
    }

    private static HttpRequestMessage Request(IntegrationConfig config, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(IntegrationKit.BaseUri(config, "url"), path));
        IntegrationKit.Bearer(request, config.Secret ?? throw new InvalidOperationException("Add a long-lived access token."));
        return request;
    }

    /// <summary>Domains and services are path segments; only allow what Home Assistant uses.</summary>
    private static string Safe(string segment) =>
        segment.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            ? segment
            : throw new ArgumentException($"'{segment}' is not a valid Home Assistant name.");
}
