using Orbs.Domain.Conversations;

namespace Orbs.Application.Conversations;

public sealed record MessageDto(Guid Id, MessageRole Role, string Content, DateTimeOffset CreatedAt);

public sealed record ConversationSummaryDto(Guid Id, Guid OrbId, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ConversationDto(
    Guid Id,
    Guid OrbId,
    string Title,
    IReadOnlyList<MessageDto> Messages,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StartConversationRequest(Guid OrbId);

public sealed record SendMessageRequest(string Content);

/// <summary>One event in a streamed reply: a text fragment, the end, or an error.</summary>
public sealed record ChatEvent(string Type, string? Text = null, Guid? MessageId = null, string? Model = null)
{
    public const string StartType = "start";
    public const string DeltaType = "delta";
    public const string DoneType = "done";
    public const string ErrorType = "error";

    public static ChatEvent Start(string model) => new(StartType, Model: model);

    public static ChatEvent Delta(string text) => new(DeltaType, Text: text);

    public static ChatEvent Done(Guid messageId) => new(DoneType, MessageId: messageId);

    public static ChatEvent Error(string message) => new(ErrorType, Text: message);
}

internal static class ConversationMapping
{
    public static ConversationSummaryDto ToSummary(this Conversation c) => new(c.Id, c.OrbId, c.Title, c.CreatedAt, c.UpdatedAt);

    public static ConversationDto ToDto(this Conversation c) => new(
        c.Id,
        c.OrbId,
        c.Title,
        [.. c.Messages.OrderBy(m => m.CreatedAt).Select(m => new MessageDto(m.Id, m.Role, m.Content, m.CreatedAt))],
        c.CreatedAt,
        c.UpdatedAt);
}
