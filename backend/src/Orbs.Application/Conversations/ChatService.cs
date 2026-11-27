using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Application.Providers;
using Orbs.Application.Tools;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Conversations;

/// <summary>Use cases for talking to orbs.</summary>
public sealed class ChatService(
    IConversationRepository conversations,
    IOrbRepository orbs,
    IModelProviderRepository providers,
    IContextSourceRepository contextSources,
    PluginRegistry<IModelAdapter> adapters,
    ProviderService providerService,
    ToolRunner tools,
    ReplyEngine engine,
    IToolApprovalRepository approvals,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const int RecentLimit = 50;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public async Task<IReadOnlyList<ConversationSummaryDto>> ListAsync(Guid? orbId, CancellationToken cancellationToken = default)
    {
        var recent = await conversations.ListRecentAsync(orbId, RecentLimit, cancellationToken);
        return [.. recent.Select(c => c.ToSummary())];
    }

    /// <summary>The conversation with its newest <paramref name="limit"/> messages.</summary>
    public async Task<ConversationDto> GetAsync(Guid id, int? limit = null, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadAsync(id, cancellationToken);
        var size = PageSize(limit);
        var (messages, hasMore) = (await conversations.ListMessagesAsync(id, null, size + 1, cancellationToken)).ToPage(size);
        var pending = (await approvals.ListPendingAsync(id, cancellationToken)).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        return conversation.ToDto() with
        {
            Messages = messages,
            HasMoreMessages = hasMore,
            PendingApproval = pending is null ? null : new ApprovalDto(pending.Id, pending.CallId, pending.Tool, pending.Arguments),
        };
    }

    /// <summary>Messages written before <paramref name="before"/>, oldest first, for scrolling back.</summary>
    public async Task<MessagePageDto> GetMessagesAsync(Guid id, DateTimeOffset? before, int? limit = null, CancellationToken cancellationToken = default)
    {
        _ = await LoadAsync(id, cancellationToken);
        var size = PageSize(limit);
        var (messages, hasMore) = (await conversations.ListMessagesAsync(id, before, size + 1, cancellationToken)).ToPage(size);
        return new MessagePageDto(messages, hasMore);
    }

    private static int PageSize(int? limit) => Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

    public async Task<ConversationDto> StartAsync(StartConversationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = await LoadOrbAsync(request.OrbId, cancellationToken);
        var conversation = Conversation.Start(request.OrbId, clock.GetUtcNow());
        conversations.Add(conversation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return conversation.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadAsync(id, cancellationToken);
        conversations.Remove(conversation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stores the user's message, then streams the orb's reply and stores it when complete.
    /// When the model asks for tools, Orbs runs them (or asks the person first), reports each call and result,
    /// and lets the model continue. Validation problems throw before streaming starts; provider failures arrive
    /// as an error event.
    /// </summary>
    public async Task<IAsyncEnumerable<ChatEvent>> SendAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversation = await LoadAsync(conversationId, cancellationToken);
        var orb = await LoadOrbAsync(conversation.OrbId, cancellationToken);
        var resolved = ModelResolver.Resolve(orb, await providers.ListAsync(cancellationToken));
        var adapter = adapters.Get(resolved.Provider.Kind);
        var context = await contextSources.ListAsync(orb.ContextSourceIds, cancellationToken);
        var toolSet = await tools.ForOrbAsync(orb, adapter.SupportsTools, cancellationToken);

        // A new message moves on from any tool call still waiting for an answer.
        foreach (var pending in await approvals.ListPendingAsync(conversation.Id, cancellationToken))
        {
            pending.Expire(clock.GetUtcNow());
        }

        conversation.AddUserMessage(request.Content, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var state = new ReplyState
        {
            ProviderId = resolved.Provider.Id,
            Model = resolved.Model,
            Turns = [.. PromptBuilder.Build(orb, context, conversation.Messages)],
        };
        var reply = new ReplyContext(conversation, orb, adapter, providerService.ConnectionFor(resolved.Provider), toolSet);
        var start = ChatEvent.Start(resolved.Model, resolved.Provider.Name, PromptBuilder.SourcesUsed(context));
        return engine.RunAsync(reply, state, start, cancellationToken);
    }

    /// <summary>
    /// Answers a tool call that waits for approval, then continues the paused reply as a new stream.
    /// "Always" also allows the tool for this orb from now on.
    /// </summary>
    public async Task<IAsyncEnumerable<ChatEvent>> DecideAsync(Guid conversationId, Guid approvalId, DecideRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversation = await LoadAsync(conversationId, cancellationToken);
        var approval = await approvals.FindAsync(approvalId, cancellationToken);
        if (approval is null || approval.ConversationId != conversationId)
        {
            throw new NotFoundException("Approval", approvalId);
        }

        var orb = await LoadOrbAsync(approval.OrbId, cancellationToken);
        var state = ReplyState.Deserialize(approval.State);
        var provider = await providers.FindAsync(state.ProviderId, cancellationToken)
            ?? throw new ModelUnavailableException("The provider that started this reply is gone.");
        var adapter = adapters.Get(provider.Kind);

        var now = clock.GetUtcNow();
        approval.Decide(request.Decision != ApprovalDecision.Deny, now);
        if (request.Decision == ApprovalDecision.Always)
        {
            orb.SetToolPermission(approval.Tool, ToolAccess.Allow, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        state.Approved = request.Decision != ApprovalDecision.Deny;
        var toolSet = await tools.ForOrbAsync(orb, adapter.SupportsTools, cancellationToken);
        var reply = new ReplyContext(conversation, orb, adapter, providerService.ConnectionFor(provider), toolSet);
        return engine.RunAsync(reply, state, ChatEvent.Resume(state.Model, provider.Name), cancellationToken);
    }

    private async Task<Conversation> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await conversations.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Conversation", id);

    private async Task<Orb> LoadOrbAsync(Guid id, CancellationToken cancellationToken) =>
        await orbs.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Orb", id);
}
