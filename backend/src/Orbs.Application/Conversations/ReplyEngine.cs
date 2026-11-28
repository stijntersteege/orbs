using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Orbs.Application.Abstractions;
using Orbs.Application.Tools;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Conversations;

public interface IToolApprovalRepository : IRepository<ToolApproval>
{
    Task<IReadOnlyList<ToolApproval>> ListPendingAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where a reply stands. Serializable, so a reply that waits for the person's approval can be stored
/// and picked up again later, even after a restart.
/// </summary>
public sealed class ReplyState
{
    public Guid ProviderId { get; set; }

    public string Model { get; set; } = string.Empty;

    public List<ChatTurn> Turns { get; set; } = [];

    public List<ToolCallDto> Records { get; set; } = [];

    public string ReplyText { get; set; } = string.Empty;

    /// <summary>Tool calls the model asked for that have not run yet, in order.</summary>
    public List<ToolCall> PendingCalls { get; set; } = [];

    public int Round { get; set; }

    /// <summary>The model gave its final answer; only pending calls (none) and saving remain.</summary>
    public bool Finished { get; set; }

    /// <summary>The person's answer for the first pending call, once they gave it.</summary>
    public bool? Approved { get; set; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public static ReplyState Deserialize(string json) => JsonSerializer.Deserialize<ReplyState>(json, Json)!;
}

/// <summary>What a running reply needs besides its state.</summary>
public sealed record ReplyContext(Conversation Conversation, Orb Orb, IModelAdapter Adapter, ProviderConnection Connection, ToolSet Tools);

/// <summary>
/// Runs a reply: stream from the model, run the tools it asks for (asking the person first when the orb's
/// settings say so), feed results back, and save the final answer with its audit trail.
/// </summary>
public sealed class ReplyEngine(ToolRunner tools, IToolApprovalRepository approvals, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async IAsyncEnumerable<ChatEvent> RunAsync(ReplyContext context, ReplyState state, ChatEvent start, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        yield return start;

        while (true)
        {
            // 1. Run the tools the model asked for, one by one.
            if (state.PendingCalls.Count > 0)
            {
                var call = state.PendingCalls[0];
                var permission = context.Orb.PermissionFor(call.Name);
                ToolCallRecord record;
                if (context.Tools.Find(call.Name) is null)
                {
                    record = new ToolCallRecord(call.Name, call.Arguments, $"There is no tool called '{call.Name}' for this orb.", true, 0, "blocked");
                }
                else if (permission == ToolAccess.Ask && state.Approved is null)
                {
                    var approval = ToolApproval.Request(context.Conversation.Id, context.Orb.Id, call.Id, call.Name, call.Arguments, state.Serialize(), clock.GetUtcNow());
                    approvals.Add(approval);
                    await unitOfWork.SaveChangesAsync(CancellationToken.None);
                    yield return ChatEvent.ApprovalRequired(approval.Id, call.Id, call.Name, call.Arguments);
                    yield break;
                }
                else if (permission == ToolAccess.Ask && state.Approved == false)
                {
                    yield return ChatEvent.ToolCalled(call.Id, call.Name, call.Arguments);
                    record = new ToolCallRecord(call.Name, call.Arguments, "The person declined this tool call. Do not try it again; answer without it.", true, 0, "denied");
                }
                else
                {
                    yield return ChatEvent.ToolCalled(call.Id, call.Name, call.Arguments);
                    var ran = await tools.RunAsync(context.Tools, context.Orb, call, cancellationToken);
                    record = new ToolCallRecord(ran.Tool, ran.Arguments, ran.Result, ran.IsError, ran.DurationMs, permission == ToolAccess.Ask ? "approved" : "allowed");
                }

                state.Approved = null;
                state.PendingCalls.RemoveAt(0);
                state.Records.Add(record.ToDto());
                state.Turns.Add(new ChatTurn(ChatRole.Tool, record.Result, ToolCallId: call.Id));
                yield return ChatEvent.ToolResult(call.Id, call.Name, record.Result, record.IsError, record.DurationMs, record.Decision);
                continue;
            }

            if (state.Finished)
            {
                break;
            }

            if (state.Round > ToolRunner.MaxRounds)
            {
                yield return ChatEvent.Error($"The orb kept asking for tools; it was stopped after {ToolRunner.MaxRounds} rounds.");
                yield break;
            }

            // 2. Ask the model, with everything so far.
            var text = new StringBuilder();
            var calls = new List<ToolCall>();
            string? error = null;
            var request = new ChatRequest(state.Model, state.Turns, context.Tools.Definitions);
            var stream = context.Adapter.StreamAsync(context.Connection, request, cancellationToken).GetAsyncEnumerator(cancellationToken);
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

            state.ReplyText += text.ToString();
            state.Round++;
            if (calls.Count == 0)
            {
                state.Finished = true;
            }
            else
            {
                state.Turns.Add(new ChatTurn(ChatRole.Assistant, text.ToString(), calls));
                state.PendingCalls.AddRange(calls);
            }
        }

        if (state.ReplyText.Length == 0)
        {
            yield return ChatEvent.Error("The model returned an empty reply.");
            yield break;
        }

        var records = state.Records.Select(r => new ToolCallRecord(r.Tool, r.Arguments, r.Result, r.IsError, r.DurationMs, r.Decision));
        var message = context.Conversation.AddAssistantMessage(state.ReplyText, clock.GetUtcNow(), state.Model, records);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        yield return ChatEvent.Done(message.Id);
    }
}
