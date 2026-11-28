using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Application.Context;
using Orbs.Application.Memories;
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
    MemoryService memories,
    ContextRetriever retriever,
    FileContextService files,
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
        foreach (var guest in request.With ?? [])
        {
            _ = await LoadOrbAsync(guest, cancellationToken);
            conversation.Invite(guest, clock.GetUtcNow());
        }

        conversations.Add(conversation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return conversation.ToDto();
    }

    public async Task<ConversationSummaryDto> RenameAsync(Guid id, RenameConversationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversation = await LoadAsync(id, cancellationToken);
        conversation.Retitle(request.Title, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return conversation.ToSummary();
    }

    /// <summary>Deletes one message from a chat, such as a reply that went wrong. The orb no longer sees it.</summary>
    public async Task DeleteMessageAsync(Guid id, Guid messageId, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadAsync(id, cancellationToken);
        if (!await conversations.RemoveMessageAsync(id, messageId, cancellationToken))
        {
            throw new NotFoundException("Message", messageId);
        }

        conversation.RemoveMessage(messageId, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadAsync(id, cancellationToken);
        conversations.Remove(conversation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stores the user's message, then streams the reply and stores it when complete. Mention other orbs with
    /// "@Name" to hand the question to them: they join the chat and answer in turn, in one stream.
    /// When the model asks for tools, Orbs runs them (or asks the person first), reports each call and result,
    /// and lets the model continue. Validation problems throw before streaming starts; provider failures arrive
    /// as an error event.
    /// </summary>
    public async Task<IAsyncEnumerable<ChatEvent>> SendAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversation = await LoadAsync(conversationId, cancellationToken);
        var allOrbs = await orbs.ListAsync(cancellationToken);
        var responders = Mentions.Find(request.Content, allOrbs);
        if (responders.Count == 0)
        {
            responders = [await LoadOrbAsync(conversation.OrbId, cancellationToken)];
        }

        // Fail before storing anything when the first orb has no model.
        _ = ModelResolver.Resolve(responders[0], await providers.ListAsync(cancellationToken));

        // A new message moves on from any tool call still waiting for an answer.
        foreach (var pending in await approvals.ListPendingAsync(conversation.Id, cancellationToken))
        {
            pending.Expire(clock.GetUtcNow());
        }

        foreach (var orb in responders)
        {
            conversation.Invite(orb.Id, clock.GetUtcNow());
        }

        conversation.AddUserMessage(request.Content, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var names = allOrbs.Where(o => conversation.OrbIds.Contains(o.Id)).ToDictionary(o => o.Id, o => o.Name);
        return ReplyAllAsync(conversation, responders, request.Content, names, cancellationToken);
    }

    /// <summary>Each responder answers in turn and sees what the ones before it said. A pause or error ends the round.</summary>
    private async IAsyncEnumerable<ChatEvent> ReplyAllAsync(
        Conversation conversation,
        IReadOnlyList<Orb> responders,
        string question,
        IReadOnlyDictionary<Guid, string> names,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var orb in responders)
        {
            var stopped = false;
            await foreach (var e in await ReplyAsync(conversation, orb, question, names, cancellationToken))
            {
                yield return e;
                stopped |= e.Type is ChatEvent.ErrorType or ChatEvent.ApprovalType;
            }

            if (stopped)
            {
                yield break;
            }
        }
    }

    private async Task<IAsyncEnumerable<ChatEvent>> ReplyAsync(Conversation conversation, Orb orb, string question, IReadOnlyDictionary<Guid, string> names, CancellationToken cancellationToken)
    {
        ResolvedModel resolved;
        try
        {
            resolved = ModelResolver.Resolve(orb, await providers.ListAsync(cancellationToken));
        }
        catch (ModelUnavailableException ex)
        {
            return One(ChatEvent.Error($"{orb.Name} has no model: {ex.Message}"));
        }

        var adapter = adapters.Get(resolved.Provider.Kind);
        var context = await contextSources.ListAsync(orb.ContextSourceIds, cancellationToken);
        var toolSet = await tools.ForOrbAsync(orb, adapter.SupportsTools, cancellationToken);
        var retrieved = await retriever.RetrieveAsync(context, question, cancellationToken);
        var images = adapter.SupportsImages ? await files.ImagesForAsync(context, cancellationToken) : [];
        var state = new ReplyState
        {
            ProviderId = resolved.Provider.Id,
            Model = resolved.Model,
            Turns = [.. PromptBuilder.Build(
                orb,
                context,
                conversation.Messages,
                memories: await memories.ForPromptAsync(orb, cancellationToken),
                retrieved: retrieved,
                images: images,
                groupNames: conversation.IsGroup ? names : null)],
        };
        var reply = new ReplyContext(conversation, orb, adapter, providerService.ConnectionFor(resolved.Provider), toolSet);
        var start = ChatEvent.StartFor(orb.Id, orb.Name, resolved.Model, resolved.Provider.Name, PromptBuilder.SourcesUsed(context, retrieved: retrieved));
        return engine.RunAsync(reply, state, start, cancellationToken);
    }

    private static async IAsyncEnumerable<ChatEvent> One(ChatEvent e)
    {
        await Task.CompletedTask;
        yield return e;
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
