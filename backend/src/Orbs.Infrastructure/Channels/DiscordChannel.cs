using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Channels;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Channels;

/// <summary>
/// An orb as a Discord bot in one text channel, read over the REST API (no gateway connection needed).
/// The bot needs the Message Content intent to read messages.
/// </summary>
internal sealed class DiscordChannel(IHttpClientFactory httpClientFactory) : IChannelAdapter
{
    public const int MaxMessageLength = 2_000;
    private static readonly Uri Api = new("https://discord.com/api/v10/");

    public string Kind => "discord";

    public string DisplayName => "Discord";

    public string Description => "Your orb as a Discord bot in one channel of your server.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("channelId", "Channel ID", Required: true, Hint: "Right-click the channel (with developer mode on) and copy its ID."),
        new("token", "Bot token", Required: true, Secret: true, Hint: "From the Discord developer portal. Turn on the Message Content intent and invite the bot to your server."),
    ];

    public async Task<string> ConnectAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        using var me = await CallAsync(config, HttpMethod.Get, "users/@me", null, cancellationToken);
        return me.RootElement.Text("username") ?? "bot";
    }

    public async Task<ChannelPoll> PollAsync(ChannelConfig config, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var path = $"channels/{config.Get("channelId")}/messages?limit=" + (cursor is null ? "1" : $"50&after={cursor}");
        using var doc = await CallAsync(config, HttpMethod.Get, path, null, cancellationToken);
        return ParseMessages(doc.RootElement, config.Get("channelId"), cursor);
    }

    /// <summary>Messages from people (not bots), oldest first. The first poll only takes the position.</summary>
    internal static ChannelPoll ParseMessages(JsonElement root, string channelId, string? cursor)
    {
        var all = root.EnumerateArray().ToList();
        var newest = all.Select(m => m.Text("id")).Where(id => id is not null).Select(id => ulong.Parse(id!, CultureInfo.InvariantCulture)).DefaultIfEmpty().Max();
        var next = newest == 0 ? cursor : newest.ToString(CultureInfo.InvariantCulture);
        if (cursor is null)
        {
            return new ChannelPoll([], next);
        }

        var messages = all
            .Where(m => !(m.TryGetProperty("author", out var a) && a.TryGetProperty("bot", out var bot) && bot.ValueKind == JsonValueKind.True))
            .Where(m => !string.IsNullOrWhiteSpace(m.Text("content")))
            .OrderBy(m => ulong.Parse(m.Text("id")!, CultureInfo.InvariantCulture))
            .Select(m => new ChannelMessage(channelId, m.GetProperty("author").Text("global_name") ?? m.GetProperty("author").Text("username") ?? "someone", m.Text("content")!))
            .ToList();
        return new ChannelPoll(messages, next);
    }

    public async Task SendAsync(ChannelConfig config, string chatId, string text, CancellationToken cancellationToken = default)
    {
        foreach (var part in TelegramChannel.Split(text, MaxMessageLength))
        {
            using var _ = await CallAsync(config, HttpMethod.Post, $"channels/{chatId}/messages", new JsonObject { ["content"] = part }, cancellationToken);
        }
    }

    public async Task<string?> SetProfileAsync(ChannelConfig config, string name, string description, byte[]? avatarPng, CancellationToken cancellationToken = default)
    {
        if (avatarPng is null)
        {
            return "Discord limits how often bots rename, so set the name in the developer portal.";
        }

        using var _ = await CallAsync(config, HttpMethod.Patch, "users/@me", new JsonObject { ["avatar"] = "data:image/png;base64," + Convert.ToBase64String(avatarPng) }, cancellationToken);
        return null;
    }

    private Task<JsonDocument> CallAsync(ChannelConfig config, HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken) =>
        ChannelHttp.SendAsync(httpClientFactory.CreateClient(Integrations.IntegrationKit.HttpClientName), method, new Uri(Api, path), config.RequireSecret(), body, cancellationToken, scheme: "Bot");
}
