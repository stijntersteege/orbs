using System.Runtime.CompilerServices;
using System.Text;
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
        return conversation.ToDto() with { Messages = messages, HasMoreMessages = hasMore };
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
    /// When the model asks for tools, Orbs runs them, reports each call and result, and lets the model continue.
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
        var toolSet = await tools.ForOrbAsync(orb, adapter.SupportsTools, cancellationToken);

        conversation.AddUserMessage(request.Content, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var reply = new ReplyRun(
            conversation,
            orb,
            adapter,
            providerService.ConnectionFor(resolved.Provider),
            resolved.Model,
            [.. PromptBuilder.Build(orb, context, conversation.Messages)],
            toolSet);
        var start = ChatEvent.Start(resolved.Model, resolved.Provider.Name, PromptBuilder.SourcesUsed(context));
        return StreamReplyAsync(reply, start, cancellationToken);
    }

    /// <summary>Everything one reply needs while it goes back and forth between the model and tools.</summary>
    private sealed record ReplyRun(
        Conversation Conversation,
        Orb Orb,
        IModelAdapter Adapter,
        ProviderConnection Connection,
        string Model,
        List<ChatTurn> Turns,
        ToolSet Tools)
    {
        public List<ToolCallRecord> Records { get; } = [];
    }

    private async IAsyncEnumerable<ChatEvent> StreamReplyAsync(ReplyRun run, ChatEvent start, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return start;

        var reply = new StringBuilder();
        for (var round = 0; round <= ToolRunner.MaxRounds; round++)
        {
            var text = new StringBuilder();
            var calls = new List<ToolCall>();
            string? error = null;
            var request = new ChatRequest(run.Model, run.Turns, run.Tools.Definitions);
            var stream = run.Adapter.StreamAsync(run.Connection, request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            try
            {
                while (true)
                {
                    ModelOutput output;
                    try
                    {
                        if (!await stream.MoveNextAsync())
                        {
                            break;
                        }

                        output = stream.Current;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        error = $"The model could not answer: {ex.Message}";
                        break;
                    }

                    if (output is TextOutput { Text.Length: > 0 } t)
                    {
                        text.Append(t.Text);
                        reply.Append(t.Text);
                        yield return ChatEvent.Delta(t.Text);
                    }
                    else if (output is ToolCallOutput c)
                    {
                        calls.Add(c.Call);
                    }
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

            if (calls.Count == 0)
            {
                break;
            }

            if (round == ToolRunner.MaxRounds)
            {
                yield return ChatEvent.Error($"The orb kept asking for tools; it was stopped after {ToolRunner.MaxRounds} rounds.");
                yield break;
            }

            run.Turns.Add(new ChatTurn(ChatRole.Assistant, text.ToString(), calls));
            foreach (var call in calls)
            {
                yield return ChatEvent.ToolCalled(call.Id, call.Name, call.Arguments);
                var record = await tools.RunAsync(run.Tools, run.Orb, call, cancellationToken);
                run.Records.Add(record);
                yield return ChatEvent.ToolResult(call.Id, call.Name, record.Result, record.IsError, record.DurationMs);
                run.Turns.Add(new ChatTurn(ChatRole.Tool, record.Result, ToolCallId: call.Id));
            }
        }

        if (reply.Length == 0)
        {
            yield return ChatEvent.Error("The model returned an empty reply.");
            yield break;
        }

        var message = run.Conversation.AddAssistantMessage(reply.ToString(), clock.GetUtcNow(), run.Model, run.Records);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        yield return ChatEvent.Done(message.Id);
    }

    private async Task<Conversation> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await conversations.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Conversation", id);

    private async Task<Orb> LoadOrbAsync(Guid id, CancellationToken cancellationToken) =>
        await orbs.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Orb", id);
}
