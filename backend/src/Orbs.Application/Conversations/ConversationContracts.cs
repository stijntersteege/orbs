using Orbs.Domain.Conversations;

namespace Orbs.Application.Conversations;

public sealed record MessageDto(
    Guid Id,
    MessageRole Role,
    string Content,
    DateTimeOffset CreatedAt,
    string? Model = null,
    IReadOnlyList<ToolCallDto>? ToolCalls = null,
    Guid? OrbId = null);

/// <summary>A tool an orb used while writing a reply: the audit trail.</summary>
public sealed record ToolCallDto(string Tool, string Arguments, string Result, bool IsError, long DurationMs, string? Decision = null);

/// <summary>A tool call waiting for the person's yes or no.</summary>
public sealed record ApprovalDto(Guid Id, string CallId, string Tool, string Arguments);

public enum ApprovalDecision { Approve, Deny, Always }

public sealed record DecideRequest(ApprovalDecision Decision);

public sealed record ConversationSummaryDto(Guid Id, Guid OrbId, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<Guid>? OrbIds = null);

/// <summary>A conversation with its newest messages. <see cref="HasMoreMessages"/> says whether older ones exist.</summary>
public sealed record ConversationDto(
    Guid Id,
    Guid OrbId,
    string Title,
    IReadOnlyList<MessageDto> Messages,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasMoreMessages = false,
    ApprovalDto? PendingApproval = null,
    IReadOnlyList<Guid>? OrbIds = null);

/// <summary>A page of older messages, oldest first.</summary>
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Messages, bool HasMore);

/// <summary>Starts a chat with one orb, or a group chat with others too.</summary>
/// <summary>Renames a chat.</summary>
public sealed record RenameConversationRequest(string Title);

public sealed record StartConversationRequest(Guid OrbId, IReadOnlyList<Guid>? With = null);

public sealed record SendMessageRequest(string Content);

/// <summary>A context source an orb read to answer, as reported in the start event.</summary>
/// <summary>A context source an orb read to answer. <see cref="Passages"/> is set when retrieval picked passages from it.</summary>
public sealed record ChatSourceDto(Guid Id, string Name, string Kind, int Characters, bool Truncated, int? Passages = null);

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
    long? DurationMs = null,
    Guid? ApprovalId = null,
    string? Decision = null,
    bool? Resumed = null,
    Guid? OrbId = null,
    string? OrbName = null)
{
    public const string StartType = "start";
    public const string DeltaType = "delta";
    public const string ToolCallType = "tool_call";
    public const string ToolResultType = "tool_result";
    public const string ApprovalType = "approval_required";
    public const string DoneType = "done";
    public const string ErrorType = "error";

    public static ChatEvent ToolCalled(string callId, string tool, string arguments) =>
        new(ToolCallType, CallId: callId, Tool: tool, Arguments: arguments);

    public static ChatEvent ToolResult(string callId, string tool, string result, bool isError, long durationMs, string? decision = null) =>
        new(ToolResultType, Text: result, CallId: callId, Tool: tool, IsError: isError, DurationMs: durationMs, Decision: decision);

    /// <summary>The reply stops here until the person answers <c>POST .../approvals/{approvalId}</c>.</summary>
    public static ChatEvent ApprovalRequired(Guid approvalId, string callId, string tool, string arguments) =>
        new(ApprovalType, ApprovalId: approvalId, CallId: callId, Tool: tool, Arguments: arguments);

    /// <summary>The start of a reply that continues after an approval.</summary>
    public static ChatEvent Resume(string model, string? provider) =>
        new(StartType, Model: model, Provider: provider, Sources: [], Resumed: true);

    public static ChatEvent Start(string model, string? provider = null, IReadOnlyList<ChatSourceDto>? sources = null) =>
        new(StartType, Model: model, Provider: provider, Sources: sources ?? []);

    /// <summary>The start of one orb's reply; in a group chat several follow each other in one stream.</summary>
    public static ChatEvent StartFor(Guid orbId, string orbName, string model, string? provider, IReadOnlyList<ChatSourceDto> sources) =>
        new(StartType, Model: model, Provider: provider, Sources: sources, OrbId: orbId, OrbName: orbName);

    public static ChatEvent Delta(string text) => new(DeltaType, Text: text);

    public static ChatEvent Done(Guid messageId) => new(DoneType, MessageId: messageId);

    public static ChatEvent Error(string message) => new(ErrorType, Text: message);
}

internal static class ConversationMapping
{
    public static ConversationSummaryDto ToSummary(this Conversation c) => new(c.Id, c.OrbId, c.Title, c.CreatedAt, c.UpdatedAt, c.OrbIds);

    public static ConversationDto ToDto(this Conversation c) => new(
        c.Id,
        c.OrbId,
        c.Title,
        [.. c.Messages.OrderBy(m => m.CreatedAt).Select(ToDto)],
        c.CreatedAt,
        c.UpdatedAt,
        OrbIds: c.OrbIds);

    public static MessageDto ToDto(this Message m) => new(
        m.Id,
        m.Role,
        m.Content,
        m.CreatedAt,
        m.Model,
        m.ToolCalls.Count == 0 ? null : [.. m.ToolCalls.Select(ToDto)],
        m.OrbId);

    public static ToolCallDto ToDto(this ToolCallRecord t) => new(t.Tool, t.Arguments, t.Result, t.IsError, t.DurationMs, t.Decision);

    /// <summary>Turns a newest-first page fetched with one extra row into an oldest-first page.</summary>
    public static (IReadOnlyList<MessageDto> Messages, bool HasMore) ToPage(this IReadOnlyList<Message> newestFirst, int limit) =>
        ([.. newestFirst.Take(limit).Reverse().Select(ToDto)], newestFirst.Count > limit);
}
