using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Common;
using Orbs.Application.Conversations;
using Orbs.Application.Tools;
using Orbs.Domain.Channels;
using Orbs.Domain.Common;

namespace Orbs.Application.Channels;

public interface IChannelRepository : IRepository<Channel>
{
    /// <summary>Every enabled channel, across all users. Called as the system.</summary>
    Task<IReadOnlyList<Channel>> ListEnabledAsync(CancellationToken cancellationToken = default);
}

/// <summary>A channel ready to use: its settings and decrypted secret.</summary>
public sealed record ChannelConfig(Guid Id, IReadOnlyDictionary<string, string> Settings, string? Secret, bool RestrictNetwork)
{
    public string Get(string key) =>
        Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException($"The channel has no '{key}' set.");

    public string? Find(string key) => Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public string RequireSecret() => Secret ?? throw new InvalidOperationException("The channel has no token or password.");
}

/// <summary>A message someone sent the orb in a messaging app.</summary>
public sealed record ChannelMessage(string ChatId, string SenderName, string Text);

/// <summary>What a poll found, and where the next poll continues.</summary>
public sealed record ChannelPoll(IReadOnlyList<ChannelMessage> Messages, string? Cursor);

/// <summary>
/// Connects orbs to one messaging app. Receiving works by polling, so Orbs needs no public address.
/// Add a class and register it to support a new app.
/// </summary>
public interface IChannelAdapter : IPlugin
{
    IReadOnlyList<IntegrationField> Fields { get; }

    /// <summary>Checks the settings and returns the address people message, such as "@pip_bot".</summary>
    Task<string> ConnectAsync(ChannelConfig config, CancellationToken cancellationToken = default);

    Task<ChannelPoll> PollAsync(ChannelConfig config, string? cursor, CancellationToken cancellationToken = default);

    Task SendAsync(ChannelConfig config, string chatId, string text, CancellationToken cancellationToken = default);

    /// <summary>Sets the bot's name, description and picture where the app allows it. Returns a note about what could not be set.</summary>
    Task<string?> SetProfileAsync(ChannelConfig config, string name, string description, byte[]? avatarPng, CancellationToken cancellationToken = default);
}

public sealed record ChannelKindDto(string Kind, string DisplayName, string Description, IReadOnlyList<IntegrationField> Fields);

public sealed record ChannelChatDto(Guid Id, string Name, DateTimeOffset PairedAt, Guid? ConversationId);

public sealed record ChannelDto(
    Guid Id,
    Guid OrbId,
    string Kind,
    string? Address,
    IReadOnlyDictionary<string, string> Settings,
    bool IsEnabled,
    string? LastError,
    IReadOnlyList<ChannelChatDto> Chats,
    DateTimeOffset CreatedAt,
    string? PairingCode = null,
    string? Note = null);

/// <summary>Connects an orb to a messaging app. A null secret on update keeps the stored one.</summary>
public sealed record SaveChannelRequest(Guid OrbId, string Kind, IReadOnlyDictionary<string, string>? Settings = null, string? Secret = null, bool IsEnabled = true);

/// <summary>Use cases for an orb's channels.</summary>
public sealed class ChannelService(
    IChannelRepository channels,
    IOrbRepository orbs,
    PluginRegistry<IChannelAdapter> adapters,
    ISecretProtector secrets,
    CurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public IReadOnlyList<ChannelKindDto> Kinds() => [.. adapters.All.Select(a => new ChannelKindDto(a.Kind, a.DisplayName, a.Description, a.Fields))];

    public async Task<IReadOnlyList<ChannelDto>> ListAsync(CancellationToken cancellationToken = default) =>
        [.. (await channels.ListAsync(cancellationToken)).OrderBy(c => c.CreatedAt).Select(ToDto)];

    /// <summary>Connects and names the bot after the orb. The answer holds the pairing code, once.</summary>
    public async Task<ChannelDto> CreateAsync(SaveChannelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orb = await orbs.FindAsync(request.OrbId, cancellationToken) ?? throw new NotFoundException("Orb", request.OrbId);
        var adapter = adapters.Get(request.Kind);
        var now = clock.GetUtcNow();
        var channel = Channel.Create(orb.Id, adapter.Kind, now);
        Apply(channel, adapter, request, now);

        var config = ConfigFor(channel);
        try
        {
            channel.Connected(await adapter.ConnectAsync(config, cancellationToken), now);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            throw new DomainException($"Could not connect: {ex.Message}");
        }

        var note = await adapter.SetProfileAsync(config, orb.Name, orb.Description, null, cancellationToken);
        var code = NewCode();
        channel.ExpectPairing(Hash(code), now);
        channels.Add(channel);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(channel) with { PairingCode = code, Note = note };
    }

    public async Task<ChannelDto> UpdateAsync(Guid id, SaveChannelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var channel = await LoadAsync(id, cancellationToken);
        Apply(channel, adapters.Get(channel.Kind), request, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(channel);
    }

    /// <summary>A fresh one-time code, for pairing another chat.</summary>
    public async Task<ChannelDto> NewPairingCodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var channel = await LoadAsync(id, cancellationToken);
        var code = NewCode();
        channel.ExpectPairing(Hash(code), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(channel) with { PairingCode = code };
    }

    public async Task UnpairAsync(Guid id, Guid chatId, CancellationToken cancellationToken = default)
    {
        var channel = await LoadAsync(id, cancellationToken);
        channel.Unpair(chatId, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Uses the orb's face (a PNG the app rendered) as the bot's picture, where the messaging app allows it.</summary>
    public async Task<string?> SetAvatarAsync(Guid id, byte[] png, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (png.Length is 0 or > 5_000_000)
        {
            throw new DomainException("The picture must be a PNG of at most 5 MB.");
        }

        var channel = await LoadAsync(id, cancellationToken);
        var orb = await orbs.FindAsync(channel.OrbId, cancellationToken) ?? throw new NotFoundException("Orb", channel.OrbId);
        return await adapters.Get(channel.Kind).SetProfileAsync(ConfigFor(channel), orb.Name, orb.Description, png, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        channels.Remove(await LoadAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public ChannelConfig ConfigFor(Channel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(channel.Settings) ?? [];
        return new ChannelConfig(channel.Id, settings, channel.ProtectedSecret is null ? null : secrets.Unprotect(channel.ProtectedSecret), RestrictNetwork: !currentUser.IsAdmin);
    }

    internal static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    /// <summary>Six letters and digits without look-alikes, such as "K7MPQ2".</summary>
    private static string NewCode()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        return string.Concat(Enumerable.Range(0, 6).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]));
    }

    private void Apply(Channel channel, IChannelAdapter adapter, SaveChannelRequest request, DateTimeOffset now)
    {
        var allowed = adapter.Fields.Where(f => !f.Secret).ToDictionary(f => f.Key, StringComparer.Ordinal);
        var settings = (request.Settings ?? new Dictionary<string, string>()).Where(p => allowed.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value.Trim(), StringComparer.Ordinal);
        foreach (var field in adapter.Fields.Where(f => f.Required && !f.Secret && !settings.ContainsKey(f.Key)))
        {
            throw new DomainException($"{field.Label} is required.");
        }

        channel.Configure(JsonSerializer.Serialize(settings), request.Secret is null ? null : request.Secret.Length == 0 ? string.Empty : secrets.Protect(request.Secret.Trim()), now);
        channel.Enable(request.IsEnabled, now);
    }

    private async Task<Channel> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await channels.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Channel", id);

    private static ChannelDto ToDto(Channel c) => new(
        c.Id,
        c.OrbId,
        c.Kind,
        c.Address,
        JsonSerializer.Deserialize<Dictionary<string, string>>(c.Settings) ?? [],
        c.IsEnabled,
        c.LastError,
        [.. c.Chats.Select(x => new ChannelChatDto(x.Id, x.Name, x.PairedAt, x.ConversationId))],
        c.CreatedAt);
}

/// <summary>
/// Moves messages between messaging apps and orbs. Polls every enabled channel (each as its owner), pairs chats
/// that send the code, ignores everyone else, and answers paired chats through the normal chat, approvals included.
/// </summary>
public sealed class ChannelRelay(IServiceScopeFactory scopes, TimeProvider clock)
{
    private static readonly string[] Yes = ["yes", "y", "ok", "allow", "ja"];
    private static readonly string[] No = ["no", "n", "deny", "nee"];

    public async Task<int> PollAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(Guid Id, Guid OwnerId)> enabled;
        await using (var scope = scopes.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<CurrentUser>().ActAsSystem();
            enabled = [.. (await scope.ServiceProvider.GetRequiredService<IChannelRepository>().ListEnabledAsync(cancellationToken)).Select(c => (c.Id, c.OwnerId))];
        }

        var handled = 0;
        foreach (var (id, ownerId) in enabled)
        {
            handled += await PollOneAsync(id, ownerId, cancellationToken);
        }

        return handled;
    }

    private async Task<int> PollOneAsync(Guid channelId, Guid ownerId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IUserRepository>().FindAsync(ownerId, cancellationToken);
        if (owner is null)
        {
            return 0;
        }

        services.GetRequiredService<CurrentUser>().SignIn(owner.Id, owner.IsAdmin);
        var channel = await services.GetRequiredService<IChannelRepository>().FindAsync(channelId, cancellationToken);
        if (channel is null)
        {
            return 0;
        }

        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var adapter = services.GetRequiredService<PluginRegistry<IChannelAdapter>>().Get(channel.Kind);
        var config = services.GetRequiredService<ChannelService>().ConfigFor(channel);
        ChannelPoll poll;
        try
        {
            poll = await adapter.PollAsync(config, channel.Cursor, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            channel.RecordError(ex.Message, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return 0;
        }

        channel.Advance(poll.Cursor, clock.GetUtcNow());
        channel.RecordError(null, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var message in poll.Messages)
        {
            try
            {
                var reply = await HandleAsync(services, channel, message, cancellationToken);
                if (reply is not null)
                {
                    await adapter.SendAsync(config, message.ChatId, reply, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                channel.RecordError(ex.Message, clock.GetUtcNow());
            }
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        return poll.Messages.Count;
    }

    private async Task<string?> HandleAsync(IServiceProvider services, Channel channel, ChannelMessage message, CancellationToken cancellationToken)
    {
        var orb = await services.GetRequiredService<IOrbRepository>().FindAsync(channel.OrbId, cancellationToken);
        if (orb is null)
        {
            return null;
        }

        var chat = channel.FindChat(message.ChatId);
        if (chat is null)
        {
            // Only a chat that sends the pairing code gets in; everyone else is ignored without a word.
            var code = message.Text.Replace("/start", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            if (channel.PairingCodeHash is null || code.Length == 0 || ChannelService.Hash(code) != channel.PairingCodeHash)
            {
                return null;
            }

            channel.Pair(message.ChatId, message.SenderName, clock.GetUtcNow());
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
            return $"Paired. You are talking to {orb.Name} now.";
        }

        var chatService = services.GetRequiredService<ChatService>();
        if (chat.ConversationId is null || !await ExistsAsync(chatService, chat.ConversationId.Value, cancellationToken))
        {
            chat.Continue((await chatService.StartAsync(new StartConversationRequest(orb.Id), cancellationToken)).Id);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        var conversationId = chat.ConversationId!.Value;
        var pending = (await chatService.GetAsync(conversationId, 1, cancellationToken)).PendingApproval;
        var answer = message.Text.Trim().ToLowerInvariant();
        IAsyncEnumerable<ChatEvent> events = pending is not null && (Yes.Contains(answer) || No.Contains(answer))
            ? await chatService.DecideAsync(conversationId, pending.Id, new DecideRequest(Yes.Contains(answer) ? ApprovalDecision.Approve : ApprovalDecision.Deny), cancellationToken)
            : await chatService.SendAsync(conversationId, new SendMessageRequest(message.Text), cancellationToken);

        var reply = new StringBuilder();
        await foreach (var e in events)
        {
            switch (e.Type)
            {
                case ChatEvent.DeltaType:
                    reply.Append(e.Text);
                    break;
                case ChatEvent.ApprovalType:
                    reply.Append(reply.Length > 0 ? "\n\n" : string.Empty)
                        .Append(orb.Name).Append(" wants to use ").Append(e.Tool).Append(" with ").Append(e.Arguments)
                        .Append(". Reply yes to allow it, or no.");
                    break;
                case ChatEvent.ErrorType:
                    reply.Append(reply.Length > 0 ? "\n\n" : string.Empty).Append("Something went wrong: ").Append(e.Text);
                    break;
            }
        }

        return reply.Length == 0 ? null : reply.ToString();
    }

    private static async Task<bool> ExistsAsync(ChatService chat, Guid conversationId, CancellationToken cancellationToken)
    {
        try
        {
            _ = await chat.GetAsync(conversationId, 1, cancellationToken);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }
}
