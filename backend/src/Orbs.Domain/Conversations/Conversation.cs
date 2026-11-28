using Orbs.Domain.Common;

namespace Orbs.Domain.Conversations;

public enum MessageRole { User, Assistant }

/// <summary>One turn in a conversation. Immutable once written.</summary>
public sealed class Message : Entity
{
    public const int MaxContentLength = 100_000;
    public const int MaxModelLength = 200;

    private readonly List<ToolCallRecord> _toolCalls = [];

    internal Message(Guid id, MessageRole role, string content, DateTimeOffset createdAt, string? model, IEnumerable<ToolCallRecord>? toolCalls, Guid? orbId = null) : base(id)
    {
        Role = role;
        Content = content;
        CreatedAt = createdAt;
        Model = model;
        OrbId = orbId;
        _toolCalls.AddRange(toolCalls ?? []);
    }

    // For EF Core materialization.
    private Message()
    {
        Content = string.Empty;
    }

    public MessageRole Role { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The orb that wrote an assistant message. Null in older chats, where it is the chat's orb.</summary>
    public Guid? OrbId { get; private set; }

    /// <summary>The model that wrote an assistant message, for the audit trail.</summary>
    public string? Model { get; private set; }

    /// <summary>Tools the orb used while writing this reply, in order: the audit trail.</summary>
    public IReadOnlyList<ToolCallRecord> ToolCalls => _toolCalls;
}

/// <summary>One tool call an orb made: what it asked for, and what came back.</summary>
public sealed class ToolCallRecord
{
    public const int MaxTextLength = 20_000;

    public ToolCallRecord(string tool, string arguments, string result, bool isError, long durationMs, string? decision = null)
    {
        Tool = Guard.Text(tool, nameof(tool), 200);
        Arguments = Clip(arguments);
        Result = Clip(result);
        IsError = isError;
        DurationMs = durationMs;
        Decision = decision;
    }

    /// <summary>Who let the call happen: "allowed" (by the orb's settings), "approved" or "denied" (by the person), or "blocked".</summary>
    public string? Decision { get; private set; }

    // For EF Core materialization.
    private ToolCallRecord()
    {
        Tool = string.Empty;
        Arguments = string.Empty;
        Result = string.Empty;
    }

    public string Tool { get; private set; }

    /// <summary>The arguments as JSON.</summary>
    public string Arguments { get; private set; }

    public string Result { get; private set; }

    public bool IsError { get; private set; }

    public long DurationMs { get; private set; }

    private static string Clip(string? text) =>
        text is null ? string.Empty : text.Length <= MaxTextLength ? text : string.Concat(text.AsSpan(0, MaxTextLength - 3), "...");
}

/// <summary>A chat between the user and one orb.</summary>
public sealed class Conversation : OwnedAggregateRoot
{
    public const int MaxTitleLength = 120;

    /// <summary>How many of the newest messages are loaded with the conversation. Older ones are read page by page.</summary>
    public const int LoadedHistory = 100;
    public const string DefaultTitle = "New chat";

    private readonly List<Message> _messages = [];

    // Other orbs in a group chat. A list (not a set) so EF Core can store it as a primitive collection.
    private readonly List<Guid> _guestOrbIds = [];

    private Conversation(Guid id, Guid orbId, DateTimeOffset now) : base(id, now)
    {
        OrbId = orbId;
    }

    // For EF Core materialization.
    private Conversation() { }

    public Guid OrbId { get; private set; }

    public string Title { get; private set; } = DefaultTitle;

    public IReadOnlyList<Message> Messages => _messages;

    /// <summary>Every orb in the chat: the one it started with, then the others who joined.</summary>
    public IReadOnlyList<Guid> OrbIds => [OrbId, .. _guestOrbIds];

    public bool IsGroup => _guestOrbIds.Count > 0;

    /// <summary>Brings another orb into the chat, such as when someone mentions it.</summary>
    public void Invite(Guid orbId, DateTimeOffset now)
    {
        Guard.NotEmpty(orbId, nameof(orbId));
        if (orbId != OrbId && !_guestOrbIds.Contains(orbId))
        {
            _guestOrbIds.Add(orbId);
            Touch(now);
        }
    }

    public static Conversation Start(Guid orbId, DateTimeOffset now, Guid? id = null)
    {
        Guard.NotEmpty(orbId, nameof(orbId));
        return new Conversation(id ?? Guid.NewGuid(), orbId, now);
    }

    /// <summary>Adds what the user said. The first message also names the chat.</summary>
    public Message AddUserMessage(string content, DateTimeOffset now)
    {
        var message = Append(MessageRole.User, content, now);
        if (Title == DefaultTitle)
        {
            Title = TitleFrom(message.Content);
        }

        return message;
    }

    /// <summary>Adds the orb's reply, with the model that wrote it and the tools it used on the way.</summary>
    public Message AddAssistantMessage(string content, DateTimeOffset now, string? model = null, IEnumerable<ToolCallRecord>? toolCalls = null, Guid? orbId = null) =>
        Append(MessageRole.Assistant, content, now, model, toolCalls, orbId);

    /// <summary>
    /// Removes a message from the loaded history (older ones are removed by the repository) and counts it as a
    /// change, so the chat moves up in the list.
    /// </summary>
    public void RemoveMessage(Guid messageId, DateTimeOffset now)
    {
        _messages.RemoveAll(m => m.Id == messageId);
        Touch(now);
    }

    public void Retitle(string title, DateTimeOffset now)
    {
        Title = Guard.Text(title, nameof(Title), MaxTitleLength);
        Touch(now);
    }

    private Message Append(MessageRole role, string content, DateTimeOffset now, string? model = null, IEnumerable<ToolCallRecord>? toolCalls = null, Guid? orbId = null)
    {
        var text = Guard.Text(content, nameof(content), Message.MaxContentLength);
        var modelName = model is null ? null : Guard.Text(model, nameof(model), Message.MaxModelLength, required: false);
        var message = new Message(Guid.NewGuid(), role, text, now, modelName, toolCalls, orbId);
        _messages.Add(message);
        Touch(now);
        return message;
    }

    private static string TitleFrom(string content)
    {
        var firstLine = content.Split('\n', 2)[0].Trim();
        return firstLine.Length <= 60 ? firstLine : string.Concat(firstLine.AsSpan(0, 57), "...");
    }
}
