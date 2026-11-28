using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orbs.Application.Channels;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Channels;

/// <summary>
/// An orb as a Matrix account, through the client-server API: /sync for new messages (rooms it is invited to are
/// joined automatically), m.room.message to answer, and the orb's name and face on its profile.
/// </summary>
internal sealed class MatrixChannel(IHttpClientFactory httpClientFactory) : IChannelAdapter
{
    private const string Filter = """{"room":{"timeline":{"limit":30}},"presence":{"not_types":["*"]},"account_data":{"not_types":["*"]}}""";
    private readonly ConcurrentDictionary<Guid, string> _userIds = new();

    public string Kind => "matrix";

    public string DisplayName => "Matrix";

    public string Description => "Your orb as a Matrix account (Element and other clients). Invite it to a room or start a chat with it.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("homeserver", "Homeserver", Required: true, Placeholder: "https://matrix.example.com"),
        new("token", "Access token", Required: true, Secret: true, Hint: "Of a separate account for the orb. In Element: Settings > Help & About > Access token."),
    ];

    public async Task<string> ConnectAsync(ChannelConfig config, CancellationToken cancellationToken = default) => await UserIdAsync(config, cancellationToken);

    public async Task<ChannelPoll> PollAsync(ChannelConfig config, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var me = await UserIdAsync(config, cancellationToken);
        var query = $"_matrix/client/v3/sync?timeout=0&filter={Uri.EscapeDataString(Filter)}" + (cursor is null ? string.Empty : $"&since={Uri.EscapeDataString(cursor)}");
        using var doc = await ChannelHttp.SendAsync(ChannelHttp.Client(httpClientFactory, config), HttpMethod.Get, new Uri(ChannelHttp.Base(config, "homeserver"), query), config.RequireSecret(), null, cancellationToken);
        var (poll, invites) = ParseSync(doc.RootElement, me, firstSync: cursor is null);
        foreach (var room in invites)
        {
            using var _ = await ChannelHttp.SendAsync(ChannelHttp.Client(httpClientFactory, config), HttpMethod.Post, new Uri(ChannelHttp.Base(config, "homeserver"), $"_matrix/client/v3/join/{Uri.EscapeDataString(room)}"), config.RequireSecret(), new JsonObject(), cancellationToken);
        }

        return poll;
    }

    /// <summary>Text messages from others, and rooms to join. The first sync only takes the position: old history is not answered.</summary>
    internal static (ChannelPoll Poll, IReadOnlyList<string> Invites) ParseSync(JsonElement root, string me, bool firstSync)
    {
        var next = root.Text("next_batch");
        var invites = new List<string>();
        var messages = new List<ChannelMessage>();
        if (root.TryGetProperty("rooms", out var rooms))
        {
            if (rooms.TryGetProperty("invite", out var invite))
            {
                invites.AddRange(invite.EnumerateObject().Select(r => r.Name));
            }

            if (!firstSync && rooms.TryGetProperty("join", out var joined))
            {
                foreach (var room in joined.EnumerateObject())
                {
                    if (!room.Value.TryGetProperty("timeline", out var timeline) || !timeline.TryGetProperty("events", out var events))
                    {
                        continue;
                    }

                    foreach (var e in events.EnumerateArray())
                    {
                        if (e.Text("type") != "m.room.message" || e.Text("sender") == me || !e.TryGetProperty("content", out var content) || content.Text("msgtype") != "m.text")
                        {
                            continue;
                        }

                        messages.Add(new ChannelMessage(room.Name, e.Text("sender") ?? "someone", content.Text("body") ?? string.Empty));
                    }
                }
            }
        }

        return (new ChannelPoll(messages, next), invites);
    }

    public async Task SendAsync(ChannelConfig config, string chatId, string text, CancellationToken cancellationToken = default)
    {
        var path = $"_matrix/client/v3/rooms/{Uri.EscapeDataString(chatId)}/send/m.room.message/orbs{Guid.NewGuid():N}";
        using var _ = await ChannelHttp.SendAsync(ChannelHttp.Client(httpClientFactory, config), HttpMethod.Put, new Uri(ChannelHttp.Base(config, "homeserver"), path), config.RequireSecret(), new JsonObject { ["msgtype"] = "m.text", ["body"] = text }, cancellationToken);
    }

    public async Task<string?> SetProfileAsync(ChannelConfig config, string name, string description, byte[]? avatarPng, CancellationToken cancellationToken = default)
    {
        var me = Uri.EscapeDataString(await UserIdAsync(config, cancellationToken));
        var homeserver = ChannelHttp.Base(config, "homeserver");
        var client = ChannelHttp.Client(httpClientFactory, config);
        using (await ChannelHttp.SendAsync(client, HttpMethod.Put, new Uri(homeserver, $"_matrix/client/v3/profile/{me}/displayname"), config.RequireSecret(), new JsonObject { ["displayname"] = name }, cancellationToken))
        {
        }

        if (avatarPng is null)
        {
            return null;
        }

        using var upload = new HttpRequestMessage(HttpMethod.Post, new Uri(homeserver, "_matrix/media/v3/upload?filename=orb.png"));
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.RequireSecret());
        upload.Content = new ByteArrayContent(avatarPng);
        upload.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var uploaded = JsonDocument.Parse(await Integrations.IntegrationKit.SendAsync(client, upload, cancellationToken));
        var contentUri = uploaded.RootElement.Text("content_uri") ?? throw new HttpRequestException("The homeserver did not return the uploaded picture.");
        using (await ChannelHttp.SendAsync(client, HttpMethod.Put, new Uri(homeserver, $"_matrix/client/v3/profile/{me}/avatar_url"), config.RequireSecret(), new JsonObject { ["avatar_url"] = contentUri }, cancellationToken))
        {
        }

        return null;
    }

    private async Task<string> UserIdAsync(ChannelConfig config, CancellationToken cancellationToken)
    {
        if (_userIds.TryGetValue(config.Id, out var known))
        {
            return known;
        }

        using var doc = await ChannelHttp.SendAsync(ChannelHttp.Client(httpClientFactory, config), HttpMethod.Get, new Uri(ChannelHttp.Base(config, "homeserver"), "_matrix/client/v3/account/whoami"), config.RequireSecret(), null, cancellationToken);
        var id = doc.RootElement.Text("user_id") ?? throw new HttpRequestException("The homeserver did not say who this is.");
        _userIds[config.Id] = id;
        return id;
    }
}
