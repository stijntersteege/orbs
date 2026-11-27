using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Channels;
using Orbs.Application.Tools;
using Orbs.Infrastructure.Integrations;

namespace Orbs.Infrastructure.Channels;

/// <summary>
/// An orb as a Telegram bot, through the Bot API with long polling (getUpdates), so no public address is needed.
/// Create the bot with @BotFather and paste its token.
/// </summary>
internal sealed class TelegramChannel(IHttpClientFactory httpClientFactory) : IChannelAdapter
{
    public const int MaxMessageLength = 4_000;

    public string Kind => "telegram";

    public string DisplayName => "Telegram";

    public string Description => "Your orb as its own Telegram bot. Make a bot with @BotFather and paste its token.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("token", "Bot token", Required: true, Secret: true, Placeholder: "123456:ABC-DEF...", Hint: "From @BotFather, after /newbot."),
    ];

    public Uri ApiBase { get; init; } = new("https://api.telegram.org/");

    public async Task<string> ConnectAsync(ChannelConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var me = await CallAsync(config, "getMe", null, cancellationToken);
        // Long polling and webhooks exclude each other; make sure no old webhook is set.
        using var _ = await CallAsync(config, "deleteWebhook", new JsonObject(), cancellationToken);
        return "@" + me.RootElement.GetProperty("result").GetProperty("username").GetString();
    }

    public async Task<ChannelPoll> PollAsync(ChannelConfig config, string? cursor, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["timeout"] = 0, ["allowed_updates"] = new JsonArray("message") };
        if (long.TryParse(cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset))
        {
            body["offset"] = offset;
        }

        using var doc = await CallAsync(config, "getUpdates", body, cancellationToken);
        return ParseUpdates(doc.RootElement, cursor);
    }

    internal static ChannelPoll ParseUpdates(JsonElement root, string? cursor)
    {
        var messages = new List<ChannelMessage>();
        long? last = null;
        foreach (var update in root.GetProperty("result").EnumerateArray())
        {
            last = update.GetProperty("update_id").GetInt64();
            if (!update.TryGetProperty("message", out var message) || !message.TryGetProperty("text", out var text))
            {
                continue;
            }

            var chatId = message.GetProperty("chat").GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);
            var from = message.TryGetProperty("from", out var sender)
                ? (sender.TryGetProperty("first_name", out var first) ? first.GetString() : null) ?? (sender.TryGetProperty("username", out var user) ? user.GetString() : null)
                : null;
            messages.Add(new ChannelMessage(chatId, from ?? "someone", text.GetString() ?? string.Empty));
        }

        return new ChannelPoll(messages, last is null ? cursor : (last.Value + 1).ToString(CultureInfo.InvariantCulture));
    }

    public async Task SendAsync(ChannelConfig config, string chatId, string text, CancellationToken cancellationToken = default)
    {
        foreach (var part in Split(text, MaxMessageLength))
        {
            using var _ = await CallAsync(config, "sendMessage", new JsonObject { ["chat_id"] = chatId, ["text"] = part }, cancellationToken);
        }
    }

    public async Task<string?> SetProfileAsync(ChannelConfig config, string name, string description, byte[]? avatarPng, CancellationToken cancellationToken = default)
    {
        using (await CallAsync(config, "setMyName", new JsonObject { ["name"] = name }, cancellationToken))
        {
        }

        using (await CallAsync(config, "setMyShortDescription", new JsonObject { ["short_description"] = Clip(description, 120) }, cancellationToken))
        {
        }

        return avatarPng is null
            ? null
            : "Telegram does not let bots change their own picture. Send the face to @BotFather with /setuserpic.";
    }

    /// <summary>Splits long replies on paragraph or line breaks, so each part fits one message.</summary>
    internal static IEnumerable<string> Split(string text, int max)
    {
        var rest = text.Trim();
        while (rest.Length > max)
        {
            var cut = rest.LastIndexOf("\n\n", max, StringComparison.Ordinal);
            if (cut < max / 2)
            {
                cut = rest.LastIndexOf('\n', max - 1);
            }

            if (cut < max / 2)
            {
                cut = max;
            }

            yield return rest[..cut].TrimEnd();
            rest = rest[cut..].TrimStart();
        }

        if (rest.Length > 0)
        {
            yield return rest;
        }
    }

    private async Task<JsonDocument> CallAsync(ChannelConfig config, string method, JsonObject? body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(IntegrationKit.HttpClientName);
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, new Uri(ApiBase, $"bot{config.RequireSecret()}/{method}"));
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        if (!response.IsSuccessStatusCode || !doc.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            var description = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : response.ReasonPhrase;
            doc.Dispose();
            throw new HttpRequestException($"Telegram said: {description}");
        }

        return doc;
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
