using Orbs.Domain.Common;

namespace Orbs.Domain.Conversations;

public enum MessageRole { User, Assistant }

/// <summary>One turn in a conversation. Immutable once written.</summary>
public sealed class Message : Entity
{
    public const int MaxContentLength = 100_000;

    internal Message(Guid id, MessageRole role, string content, DateTimeOffset createdAt) : base(id)
    {
        Role = role;
        Content = content;
        CreatedAt = createdAt;
    }

    // For EF Core materialization.
    private Message()
    {
        Content = string.Empty;
    }

    public MessageRole Role { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>A chat between the user and one orb.</summary>
public sealed class Conversation : OwnedAggregateRoot
{
    public const int MaxTitleLength = 120;

    /// <summary>How many of the newest messages are loaded with the conversation. Older ones are read page by page.</summary>
    public const int LoadedHistory = 100;
    public const string DefaultTitle = "New chat";

    private readonly List<Message> _messages = [];

    private Conversation(Guid id, Guid orbId, DateTimeOffset now) : base(id, now)
    {
        OrbId = orbId;
    }

    // For EF Core materialization.
    private Conversation() { }

    public Guid OrbId { get; private set; }

    public string Title { get; private set; } = DefaultTitle;

    public IReadOnlyList<Message> Messages => _messages;

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

    public Message AddAssistantMessage(string content, DateTimeOffset now) => Append(MessageRole.Assistant, content, now);

    public void Retitle(string title, DateTimeOffset now)
    {
        Title = Guard.Text(title, nameof(Title), MaxTitleLength);
        Touch(now);
    }

    private Message Append(MessageRole role, string content, DateTimeOffset now)
    {
        var text = Guard.Text(content, nameof(content), Message.MaxContentLength);
        var message = new Message(Guid.NewGuid(), role, text, now);
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
