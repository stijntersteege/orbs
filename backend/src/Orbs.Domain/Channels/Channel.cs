using Orbs.Domain.Common;

namespace Orbs.Domain.Channels;

/// <summary>
/// An orb reachable in a messaging app, such as its own Telegram bot. People pair a chat once with a
/// one-time code; messages from unpaired chats are ignored, so nobody else can talk to your orb.
/// </summary>
public sealed class Channel : OwnedAggregateRoot
{
    public const int MaxSettingsLength = 4_000;

    private readonly List<ChannelChat> _chats = [];

    private Channel(Guid id, Guid orbId, string kind, DateTimeOffset now) : base(id, now)
    {
        OrbId = orbId;
        Kind = kind;
    }

    // For EF Core materialization.
    private Channel()
    {
        Kind = string.Empty;
    }

    public Guid OrbId { get; private set; }

    /// <summary>The adapter, such as <c>telegram</c>.</summary>
    public string Kind { get; private set; }

    /// <summary>Plain settings as JSON, such as a homeserver address.</summary>
    public string Settings { get; private set; } = "{}";

    /// <summary>The bot token or password, encrypted.</summary>
    public string? ProtectedSecret { get; private set; }

    /// <summary>The account people message, such as "@pip_orbs_bot".</summary>
    public string? Address { get; private set; }

    /// <summary>Where the last poll stopped, so no message is read twice.</summary>
    public string? Cursor { get; private set; }

    /// <summary>A hash of the one-time code that pairs a chat. Null once used.</summary>
    public string? PairingCodeHash { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public string? LastError { get; private set; }

    public IReadOnlyList<ChannelChat> Chats => _chats;

    public static Channel Create(Guid orbId, string kind, DateTimeOffset now)
    {
        Guard.NotEmpty(orbId, nameof(orbId));
        return new Channel(Guid.NewGuid(), orbId, Slug.Normalize(kind, nameof(Kind)), now);
    }

    public void Configure(string? settingsJson, string? protectedSecret, DateTimeOffset now)
    {
        Settings = Guard.Text(settingsJson ?? "{}", nameof(Settings), MaxSettingsLength);
        if (protectedSecret is not null)
        {
            ProtectedSecret = protectedSecret.Length == 0 ? null : protectedSecret;
        }

        Touch(now);
    }

    public void Connected(string address, DateTimeOffset now)
    {
        Address = Guard.Text(address, nameof(Address), 200);
        LastError = null;
        Touch(now);
    }

    public void ExpectPairing(string codeHash, DateTimeOffset now)
    {
        PairingCodeHash = Guard.Text(codeHash, nameof(codeHash), 128);
        Touch(now);
    }

    /// <summary>Links a chat to the orb. Uses up the pairing code.</summary>
    public ChannelChat Pair(string externalChatId, string? name, DateTimeOffset now)
    {
        var chat = FindChat(externalChatId) ?? new ChannelChat(Guid.NewGuid(), Guard.Text(externalChatId, nameof(externalChatId), 200), (name ?? string.Empty).Trim(), now);
        if (!_chats.Contains(chat))
        {
            _chats.Add(chat);
        }

        PairingCodeHash = null;
        Touch(now);
        return chat;
    }

    public void Unpair(Guid chatId, DateTimeOffset now)
    {
        _chats.RemoveAll(c => c.Id == chatId);
        Touch(now);
    }

    public ChannelChat? FindChat(string externalChatId) => _chats.FirstOrDefault(c => c.ExternalChatId == externalChatId);

    public void Advance(string? cursor, DateTimeOffset now)
    {
        Cursor = cursor;
        Touch(now);
    }

    public void Enable(bool enabled, DateTimeOffset now)
    {
        IsEnabled = enabled;
        Touch(now);
    }

    public void RecordError(string? error, DateTimeOffset now)
    {
        LastError = error is null ? null : error.Length <= 1_000 ? error : error[..1_000];
        Touch(now);
    }
}

/// <summary>A paired chat in the messaging app and the Orbs conversation it continues.</summary>
public sealed class ChannelChat : Entity
{
    internal ChannelChat(Guid id, string externalChatId, string name, DateTimeOffset pairedAt) : base(id)
    {
        ExternalChatId = externalChatId;
        Name = name.Length <= 200 ? name : name[..200];
        PairedAt = pairedAt;
    }

    // For EF Core materialization.
    private ChannelChat()
    {
        ExternalChatId = string.Empty;
        Name = string.Empty;
    }

    public string ExternalChatId { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset PairedAt { get; private set; }

    public Guid? ConversationId { get; private set; }

    public void Continue(Guid conversationId) => ConversationId = conversationId;
}
