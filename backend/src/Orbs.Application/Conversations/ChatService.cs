using System.Runtime.CompilerServices;
using System.Text;
using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Application.Providers;
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
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const int RecentLimit = 50;

    public async Task<IReadOnlyList<ConversationSummaryDto>> ListAsync(Guid? orbId, CancellationToken cancellationToken = default)
    {
        var recent = await conversations.ListRecentAsync(orbId, RecentLimit, cancellationToken);
        return [.. recent.Select(c => c.ToSummary())];
    }

    public async Task<ConversationDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToDto();

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
    /// Validation problems throw before streaming starts; provider failures arrive as an error event.
    /// </summary>
    public async Task<IAsyncEnumerable<ChatEvent>> SendAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversation = await LoadAsync(conversationId, cancellationToken);
        var orb = await LoadOrbAsync(conversation.OrbId, cancellationToken);
        var resolved = ModelResolver.Resolve(orb, await providers.ListAsync(cancellationToken));
        var adapter = adapters.Get(resolved.Provider.Kind);
        var context = await contextSources.ListAsync(orb.ContextSourceIds, cancellationToken);

        conversation.AddUserMessage(request.Content, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var chatRequest = new ChatRequest(resolved.Model, PromptBuilder.Build(orb, context, conversation.Messages));
        var connection = providerService.ConnectionFor(resolved.Provider);
        return StreamReplyAsync(conversation, adapter, connection, chatRequest, cancellationToken);
    }

    private async IAsyncEnumerable<ChatEvent> StreamReplyAsync(
        Conversation conversation,
        IModelAdapter adapter,
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return ChatEvent.Start(request.Model);

        var reply = new StringBuilder();
        string? error = null;
        var stream = adapter.StreamChatAsync(connection, request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                string fragment;
                try
                {
                    if (!await stream.MoveNextAsync())
                    {
                        break;
                    }

                    fragment = stream.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    error = $"The model could not answer: {ex.Message}";
                    break;
                }

                reply.Append(fragment);
                yield return ChatEvent.Delta(fragment);
            }
        }
        finally
        {
            await stream.DisposeAsync();
        }

        if (error is not null)
        {
            yield return ChatEvent.Error(error);
            yield break;
        }

        if (reply.Length == 0)
        {
            yield return ChatEvent.Error("The model returned an empty reply.");
            yield break;
        }

        var message = conversation.AddAssistantMessage(reply.ToString(), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        yield return ChatEvent.Done(message.Id);
    }

    private async Task<Conversation> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await conversations.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Conversation", id);

    private async Task<Orb> LoadOrbAsync(Guid id, CancellationToken cancellationToken) =>
        await orbs.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Orb", id);
}
