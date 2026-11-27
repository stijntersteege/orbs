using Orbs.Domain.Common;

namespace Orbs.Domain.Conversations;

public enum ApprovalStatus { Pending, Approved, Denied, Expired }

/// <summary>
/// A tool call waiting for the person to say yes or no. The reply stops at this point;
/// <see cref="State"/> holds everything needed to pick it up again once they decide.
/// </summary>
public sealed class ToolApproval : OwnedAggregateRoot
{
    public const int MaxStateLength = 2_000_000;

    private ToolApproval(Guid id, Guid conversationId, Guid orbId, string callId, string tool, string arguments, string state, DateTimeOffset now)
        : base(id, now)
    {
        ConversationId = conversationId;
        OrbId = orbId;
        CallId = callId;
        Tool = tool;
        Arguments = arguments;
        State = state;
    }

    // For EF Core materialization.
    private ToolApproval()
    {
        CallId = string.Empty;
        Tool = string.Empty;
        Arguments = string.Empty;
        State = string.Empty;
    }

    public Guid ConversationId { get; private set; }

    public Guid OrbId { get; private set; }

    public string CallId { get; private set; }

    public string Tool { get; private set; }

    /// <summary>What the orb wants to pass to the tool, as JSON.</summary>
    public string Arguments { get; private set; }

    /// <summary>The paused reply, serialized by the application layer.</summary>
    public string State { get; private set; }

    public ApprovalStatus Status { get; private set; } = ApprovalStatus.Pending;

    public DateTimeOffset? DecidedAt { get; private set; }

    public static ToolApproval Request(Guid conversationId, Guid orbId, string callId, string tool, string arguments, string state, DateTimeOffset now)
    {
        Guard.NotEmpty(conversationId, nameof(conversationId));
        Guard.NotEmpty(orbId, nameof(orbId));
        return new ToolApproval(
            Guid.NewGuid(),
            conversationId,
            orbId,
            Guard.Text(callId, nameof(callId), 200),
            Guard.Text(tool, nameof(tool), 200),
            arguments ?? "{}",
            Guard.Text(state, nameof(state), MaxStateLength),
            now);
    }

    public void Decide(bool approve, DateTimeOffset now)
    {
        if (Status != ApprovalStatus.Pending)
        {
            throw new DomainException("This request was already answered.");
        }

        Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Denied;
        DecidedAt = now;
        Touch(now);
    }

    /// <summary>The person moved on (sent another message), so the paused reply will not continue.</summary>
    public void Expire(DateTimeOffset now)
    {
        if (Status == ApprovalStatus.Pending)
        {
            Status = ApprovalStatus.Expired;
            DecidedAt = now;
            Touch(now);
        }
    }
}
