using System.Text;
using Orbs.Application.Abstractions;
using Orbs.Application.Context;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Memories;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Conversations;

/// <summary>Limits that keep prompts inside a typical model's context window.</summary>
public sealed record PromptBudget(int MaxContextCharacters = 48_000, int MaxHistoryMessages = 40)
{
    public static PromptBudget Default { get; } = new();
}

/// <summary>
/// Turns an orb, its context and a conversation into the messages sent to a model.
/// Pure: no I/O, so it is easy to test and to swap for a retrieval-based builder later.
/// </summary>
public static class PromptBuilder
{
    public static IReadOnlyList<ChatTurn> Build(
        Orb orb,
        IReadOnlyList<ContextSource> context,
        IReadOnlyList<Message> history,
        PromptBudget? budget = null,
        IReadOnlyList<Memory>? memories = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? retrieved = null)
    {
        ArgumentNullException.ThrowIfNull(orb);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(history);
        budget ??= PromptBudget.Default;

        var turns = new List<ChatTurn> { new(ChatRole.System, SystemPrompt(orb, context, budget, memories ?? [], retrieved)) };
        turns.AddRange(history
            .OrderBy(m => m.CreatedAt)
            .TakeLast(budget.MaxHistoryMessages)
            .Select(m => new ChatTurn(m.Role == MessageRole.User ? ChatRole.User : ChatRole.Assistant, m.Content)));
        return turns;
    }

    internal static string SystemPrompt(
        Orb orb,
        IReadOnlyList<ContextSource> context,
        PromptBudget budget,
        IReadOnlyList<Memory>? memories = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? retrieved = null)
    {
        var prompt = new StringBuilder();
        prompt.Append("You are ").Append(orb.Name).Append(", an AI companion in Orbs.");
        if (orb.Description.Length > 0)
        {
            prompt.Append(' ').Append(orb.Description.TrimEnd('.')).Append('.');
        }

        prompt.AppendLine();

        if (orb.Instructions.Length > 0)
        {
            prompt.AppendLine().AppendLine("## How to behave").AppendLine(orb.Instructions);
        }

        if (memories is { Count: > 0 })
        {
            prompt.AppendLine()
                .AppendLine("## What you remember")
                .AppendLine("Facts you saved in earlier chats or the person told you to keep. Use them when they help.");
            foreach (var memory in memories)
            {
                prompt.Append("- ").AppendLine(memory.Content);
            }
        }

        var ready = Readable(context);
        if (ready.Count > 0)
        {
            prompt.AppendLine()
                .AppendLine("## What you know")
                .AppendLine("Use these sources when they help. Say which source you used. If they do not cover the question, say so.");

            var whole = ready.Where(s => retrieved?.ContainsKey(s.Id) != true).ToList();
            var share = whole.Count == 0 ? 0 : budget.MaxContextCharacters / whole.Count;
            foreach (var source in ready)
            {
                string content;
                if (retrieved is not null && retrieved.TryGetValue(source.Id, out var passages))
                {
                    if (passages.Count == 0)
                    {
                        continue; // Retrieval found nothing relevant in this source.
                    }

                    content = "Passages that match the question:\n" + ContextRetriever.Join(passages);
                }
                else
                {
                    content = source.Content.Length > share ? string.Concat(source.Content.AsSpan(0, share), " [truncated]") : source.Content;
                }

                prompt.AppendLine().Append("### ").Append(source.Name).Append(" (").Append(source.Kind).AppendLine(")").AppendLine(content);
            }
        }

        return prompt.ToString().TrimEnd();
    }

    /// <summary>
    /// The sources that end up in the prompt, with how much of each the model sees.
    /// Same rules as <see cref="Build"/>, so what people are shown is what the model read.
    /// </summary>
    public static IReadOnlyList<ChatSourceDto> SourcesUsed(
        IReadOnlyList<ContextSource> context,
        PromptBudget? budget = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? retrieved = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        budget ??= PromptBudget.Default;
        var ready = Readable(context);
        if (ready.Count == 0)
        {
            return [];
        }

        var whole = ready.Count(s => retrieved?.ContainsKey(s.Id) != true);
        var share = whole == 0 ? 0 : budget.MaxContextCharacters / whole;
        var used = new List<ChatSourceDto>();
        foreach (var s in ready)
        {
            if (retrieved is not null && retrieved.TryGetValue(s.Id, out var passages))
            {
                if (passages.Count > 0)
                {
                    used.Add(new ChatSourceDto(s.Id, s.Name, s.Kind, passages.Sum(p => p.Length), false, passages.Count));
                }
            }
            else
            {
                used.Add(new ChatSourceDto(s.Id, s.Name, s.Kind, Math.Min(s.Content.Length, share), s.Content.Length > share));
            }
        }

        return used;
    }

    private static List<ContextSource> Readable(IReadOnlyList<ContextSource> context) =>
        [.. context.Where(s => s.Status == ContextSourceStatus.Ready && s.Content.Length > 0)];
}
