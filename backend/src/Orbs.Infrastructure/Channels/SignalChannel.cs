using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Channels;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Channels;

/// <summary>
/// An orb on Signal through a self-hosted signal-cli REST API (github.com/bbernhard/signal-cli-rest-api),
/// with its own phone number registered there.
/// </summary>
internal sealed class SignalChannel(IHttpClientFactory httpClientFactory) : IChannelAdapter
{
    public string Kind => "signal";

    public string DisplayName => "Signal";

    public string Description => "Your orb on Signal, through your own signal-cli REST API with a number for the orb.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("apiUrl", "signal-cli REST API address", Required: true, Placeholder: "http://signal-cli:8080"),
        new("number", "The orb's number", Required: true, Placeholder: "+31612345678", Hint: "Registered or linked in signal-cli first."),
        new("token", "Access token", Secret: true, Hint: "Only if you put the API behind a proxy that wants a bearer token."),
    ];

    public async Task<string> ConnectAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var _ = await CallAsync(config, HttpMethod.Get, "v1/about", null, cancellationToken);
        return config.Get("number");
    }

    public async Task<ChannelPoll> PollAsync(ChannelConfig config, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var doc = await CallAsync(config, HttpMethod.Get, $"v1/receive/{Uri.EscapeDataString(config.Get("number"))}", null, cancellationToken);
        return new ChannelPoll(ParseReceived(doc.RootElement), cursor);
    }

    /// <summary>Direct text messages; receipts, typing notices and group messages are skipped.</summary>
    internal static IReadOnlyList<ChannelMessage> ParseReceived(JsonElement root)
    {
        var messages = new List<ChannelMessage>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            return messages;
        }

        foreach (var item in root.EnumerateArray())
        {
            if (!item.TryGetProperty("envelope", out var envelope) || !envelope.TryGetProperty("dataMessage", out var data))
            {
                continue;
            }

            var text = data.Text("message");
            var from = envelope.Text("sourceNumber") ?? envelope.Text("source");
            if (string.IsNullOrWhiteSpace(text) || from is null || data.TryGetProperty("groupInfo", out _))
            {
                continue;
            }

            messages.Add(new ChannelMessage(from, envelope.Text("sourceName") ?? from, text));
        }

        return messages;
    }

    public async Task SendAsync(ChannelConfig config, string chatId, string text, CancellationToken cancellationToken = default)
    {
        using var _ = await CallAsync(config, HttpMethod.Post, "v2/send", new JsonObject
        {
            ["message"] = text,
            ["number"] = config.Get("number"),
            ["recipients"] = new JsonArray(chatId),
        }, cancellationToken);
    }

    public async Task<string?> SetProfileAsync(ChannelConfig config, string name, string description, byte[]? avatarPng, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["name"] = name, ["about"] = description };
        if (avatarPng is not null)
        {
            body["base64_avatar"] = Convert.ToBase64String(avatarPng);
        }

        using var _ = await CallAsync(config, HttpMethod.Put, $"v1/profiles/{Uri.EscapeDataString(config.Get("number"))}", body, cancellationToken);
        return null;
    }

    private Task<JsonDocument> CallAsync(ChannelConfig config, HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken) =>
        ChannelHttp.SendAsync(ChannelHttp.Client(httpClientFactory, config), method, new Uri(ChannelHttp.Base(config, "apiUrl"), path), config.Secret, body, cancellationToken);
}
