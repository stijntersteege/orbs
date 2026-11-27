using System.Text;
using System.Text.Json;
using Orbs.Application.Abstractions;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Integrations;

/// <summary>Web search through a self-hosted SearXNG instance or the Brave Search API.</summary>
internal sealed class WebSearchIntegration(IHttpClientFactory httpClientFactory) : IIntegrationKind
{
    public string Kind => "web-search";

    public string DisplayName => "Web search";

    public string Description => "Search the web with your own SearXNG instance or the Brave Search API.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("provider", "Search engine", Required: true, Options: ["searxng", "brave"]),
        new("url", "SearXNG address", Placeholder: "https://search.example.com", Hint: "Only for SearXNG. JSON output must be enabled."),
        new("apiKey", "Brave API key", Secret: true, Hint: "Only for Brave Search."),
    ];

    public IReadOnlyList<ITool> CreateTools(IntegrationConfig config) =>
    [
        new DelegateTool(
            "web_search",
            "Web search",
            "Search the web. Returns the top results with title, address and a snippet.",
            """{"type":"object","properties":{"query":{"type":"string"},"count":{"type":"integer","description":"How many results, 1 to 10. Default 5."}},"required":["query"]}""",
            ToolRisk.Read,
            (args, ct) => SearchAsync(config, args.Required("query"), args.Int("count", 5, 1, 10), ct)),
    ];

    public Task TestAsync(IntegrationConfig config, CancellationToken cancellationToken = default) =>
        SearchAsync(config, "orbs", 1, cancellationToken);

    internal async Task<string> SearchAsync(IntegrationConfig config, string query, int count, CancellationToken cancellationToken)
    {
        var client = IntegrationKit.Http(httpClientFactory, config);
        var results = config.Find("provider") == "brave"
            ? await BraveAsync(client, config, query, count, cancellationToken)
            : await SearxngAsync(client, config, query, count, cancellationToken);
        return Format(results);
    }

    internal static string Format(IReadOnlyList<(string Title, string Url, string Snippet)> results)
    {
        if (results.Count == 0)
        {
            return "No results.";
        }

        var text = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            text.Append(i + 1).Append(". ").AppendLine(results[i].Title).AppendLine(results[i].Url);
            if (results[i].Snippet.Length > 0)
            {
                text.AppendLine(IntegrationKit.Clip(results[i].Snippet, 300));
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    private static async Task<List<(string, string, string)>> SearxngAsync(HttpClient client, IntegrationConfig config, string query, int count, CancellationToken cancellationToken)
    {
        var uri = new Uri(IntegrationKit.BaseUri(config, "url"), $"search?q={Uri.EscapeDataString(query)}&format=json");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var doc = JsonDocument.Parse(await IntegrationKit.SendAsync(client, request, cancellationToken));
        return [.. doc.RootElement.GetProperty("results").EnumerateArray().Take(count)
            .Select(r => (r.Text("title") ?? "", r.Text("url") ?? "", r.Text("content") ?? ""))];
    }

    private static async Task<List<(string, string, string)>> BraveAsync(HttpClient client, IntegrationConfig config, string query, int count, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={count}");
        request.Headers.Add("X-Subscription-Token", config.Secret ?? throw new InvalidOperationException("Add your Brave API key."));
        request.Headers.Add("Accept", "application/json");
        using var doc = JsonDocument.Parse(await IntegrationKit.SendAsync(client, request, cancellationToken));
        if (!doc.RootElement.TryGetProperty("web", out var web) || !web.TryGetProperty("results", out var results))
        {
            return [];
        }

        return [.. results.EnumerateArray().Take(count).Select(r => (r.Text("title") ?? "", r.Text("url") ?? "", r.Text("description") ?? ""))];
    }
}
