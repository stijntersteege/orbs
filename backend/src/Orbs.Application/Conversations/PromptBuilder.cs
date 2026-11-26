using System.Text;
using Orbs.Application.Abstractions;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
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
        PromptBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(orb);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(history);
        budget ??= PromptBudget.Default;

        var turns = new List<ChatTurn> { new(ChatRole.System, SystemPrompt(orb, context, budget)) };
        turns.AddRange(history
            .OrderBy(m => m.CreatedAt)
            .TakeLast(budget.MaxHistoryMessages)
            .Select(m => new ChatTurn(m.Role == MessageRole.User ? ChatRole.User : ChatRole.Assistant, m.Content)));
        return turns;
    }

    internal static string SystemPrompt(Orb orb, IReadOnlyList<ContextSource> context, PromptBudget budget)
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

        var ready = context.Where(s => s.Status == ContextSourceStatus.Ready && s.Content.Length > 0).ToList();
        if (ready.Count > 0)
        {
            prompt.AppendLine()
                .AppendLine("## What you know")
                .AppendLine("Use these sources when they help. Say which source you used. If they do not cover the question, say so.");

            var share = budget.MaxContextCharacters / ready.Count;
            foreach (var source in ready)
            {
                var content = source.Content.Length > share ? string.Concat(source.Content.AsSpan(0, share), " [truncated]") : source.Content;
                prompt.AppendLine().Append("### ").Append(source.Name).Append(" (").Append(source.Kind).AppendLine(")").AppendLine(content);
            }
        }

        return prompt.ToString().TrimEnd();
    }
}
