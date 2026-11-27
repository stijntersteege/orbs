using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Domain.Orbs;

namespace Orbs.Testing;

/// <summary>Typed calls to the Orbs API, shared by integration tests and specs.</summary>
public sealed class ApiClient(HttpClient http)
{
    public static JsonSerializerOptions Json { get; } = CreateJsonOptions();

    public HttpClient Http { get; } = http;

    public async Task<OrbDto> CreateOrbAsync(SaveOrbRequest request) =>
        await ReadAsync<OrbDto>(await Http.PostAsJsonAsync("/api/orbs", request, Json));

    public async Task<ProviderDto> CreateProviderAsync(SaveProviderRequest request) =>
        await ReadAsync<ProviderDto>(await Http.PostAsJsonAsync("/api/providers", request, Json));

    public Task<ProviderDto> ConnectEchoAsync() =>
        CreateProviderAsync(new SaveProviderRequest("Echo", "echo", DefaultModel: "echo-1"));

    public async Task<ContextSourceDto> AddNoteAsync(string name, string text) =>
        await ReadAsync<ContextSourceDto>(await Http.PostAsJsonAsync("/api/context-sources", new SaveContextSourceRequest(name, "text", Input: text), Json));

    public async Task<ConversationDto> StartConversationAsync(Guid orbId) =>
        await ReadAsync<ConversationDto>(await Http.PostAsJsonAsync("/api/conversations", new StartConversationRequest(orbId), Json));

    /// <summary>Sets one tool's permission on an orb, keeping everything else.</summary>
    public async Task<OrbDto> SetToolAccessAsync(OrbDto orb, string tool, ToolAccess access)
    {
        ArgumentNullException.ThrowIfNull(orb);
        var permissions = new Dictionary<string, ToolAccess>(orb.ToolPermissions ?? new Dictionary<string, ToolAccess>()) { [tool] = access };
        var request = new SaveOrbRequest(orb.Name, orb.Description, orb.Instructions, orb.Appearance, orb.Model, orb.ContextSourceIds, permissions);
        return await ReadAsync<OrbDto>(await Http.PutAsJsonAsync($"/api/orbs/{orb.Id}", request, Json));
    }

    /// <summary>Answers a waiting tool call and collects the rest of the reply.</summary>
    public async Task<SendResult> DecideAsync(Guid conversationId, Guid approvalId, ApprovalDecision decision)
    {
        var response = await Http.PostAsJsonAsync($"/api/conversations/{conversationId}/approvals/{approvalId}", new DecideRequest(decision), Json);
        return await CollectAsync(response);
    }

    public async Task<T> GetAsync<T>(string path) => await ReadAsync<T>(await Http.GetAsync(new Uri(path, UriKind.Relative)));

    /// <summary>Sends a message and collects every server-sent event of the reply.</summary>
    public async Task<SendResult> SendAsync(Guid conversationId, string content)
    {
        var response = await Http.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendMessageRequest(content), Json);
        return await CollectAsync(response);
    }

    private static async Task<SendResult> CollectAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return new SendResult(response, []);
        }

        var events = new List<ChatEvent>();
        var body = await response.Content.ReadAsStringAsync();
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                events.Add(JsonSerializer.Deserialize<ChatEvent>(line["data:".Length..].Trim(), Json)!);
            }
        }

        return new SendResult(response, events);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)response.StatusCode}: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public sealed record SendResult(HttpResponseMessage Response, IReadOnlyList<ChatEvent> Events)
{
    public string Reply => string.Concat(Events.Where(e => e.Type == ChatEvent.DeltaType).Select(e => e.Text));
}
