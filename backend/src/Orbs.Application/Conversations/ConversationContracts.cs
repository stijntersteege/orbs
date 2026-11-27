using Orbs.Domain.Conversations;

namespace Orbs.Application.Conversations;

public sealed record MessageDto(
    Guid Id,
    MessageRole Role,
    string Content,
    DateTimeOffset CreatedAt,
    string? Model = null,
    IReadOnlyList<ToolCallDto>? ToolCalls = null);

/// <summary>A tool an orb used while writing a reply: the audit trail.</summary>
public sealed record ToolCallDto(string Tool, string Arguments, string Result, bool IsError, long DurationMs);

public sealed record ConversationSummaryDto(Guid Id, Guid OrbId, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>A conversation with its newest messages. <see cref="HasMoreMessages"/> says whether older ones exist.</summary>
public sealed record ConversationDto(
    Guid Id,
    Guid OrbId,
    string Title,
    IReadOnlyList<MessageDto> Messages,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasMoreMessages = false);

/// <summary>A page of older messages, oldest first.</summary>
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Messages, bool HasMore);

public sealed record StartConversationRequest(Guid OrbId);

public sealed record SendMessageRequest(string Content);

/// <summary>A context source an orb read to answer, as reported in the start event.</summary>
public sealed record ChatSourceDto(Guid Id, string Name, string Kind, int Characters, bool Truncated);

/// <summary>
/// One event in a streamed reply: the start (which model and provider answer, and which sources they read),
/// a text fragment, a tool call and its result, the end, or an error.
/// </summary>
public sealed record ChatEvent(
    string Type,
    string? Text = null,
    Guid? MessageId = null,
    string? Model = null,
    string? Provider = null,
    IReadOnlyList<ChatSourceDto>? Sources = null,
    string? CallId = null,
    string? Tool = null,
    string? Arguments = null,
    bool? IsError = null,
    long? DurationMs = null)
{
    public const string StartType = "start";
    public const string DeltaType = "delta";
    public const string ToolCallType = "tool_call";
    public const string ToolResultType = "tool_result";
    public const string DoneType = "done";
    public const string ErrorType = "error";

    public static ChatEvent ToolCalled(string callId, string tool, string arguments) =>
        new(ToolCallType, CallId: callId, Tool: tool, Arguments: arguments);

    public static ChatEvent ToolResult(string callId, string tool, string result, bool isError, long durationMs) =>
        new(ToolResultType, Text: result, CallId: callId, Tool: tool, IsError: isError, DurationMs: durationMs);

    public static ChatEvent Start(string model, string? provider = null, IReadOnlyList<ChatSourceDto>? sources = null) =>
        new(StartType, Model: model, Provider: provider, Sources: sources ?? []);

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
        [.. c.Messages.OrderBy(m => m.CreatedAt).Select(ToDto)],
        c.CreatedAt,
        c.UpdatedAt);

    public static MessageDto ToDto(this Message m) => new(
        m.Id,
        m.Role,
        m.Content,
        m.CreatedAt,
        m.Model,
        m.ToolCalls.Count == 0 ? null : [.. m.ToolCalls.Select(t => new ToolCallDto(t.Tool, t.Arguments, t.Result, t.IsError, t.DurationMs))]);

    /// <summary>Turns a newest-first page fetched with one extra row into an oldest-first page.</summary>
    public static (IReadOnlyList<MessageDto> Messages, bool HasMore) ToPage(this IReadOnlyList<Message> newestFirst, int limit) =>
        ([.. newestFirst.Take(limit).Reverse().Select(ToDto)], newestFirst.Count > limit);
}
