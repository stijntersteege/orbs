using System.Text;
using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Memories;
using Orbs.Application.Providers;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Orbs;

/// <summary>
/// Lets an orb act as a coordinator: it hands a question to one of the person's other orbs, a specialist, and
/// reads its answer. The specialist answers with its own brief, context and memories, but without tools, so a
/// delegated question never acts on anything or waits for an approval nobody can see.
/// </summary>
public sealed class AskOrbTool(
    IOrbRepository orbs,
    IModelProviderRepository providers,
    IContextSourceRepository contextSources,
    PluginRegistry<IModelAdapter> adapters,
    ProviderService providerService,
    MemoryService memories,
    ContextRetriever retriever,
    TimeProvider clock) : ITool
{
    /// <summary>The longest answer a specialist hands back, so one delegation cannot fill the coordinator's prompt.</summary>
    public const int MaxAnswerLength = 8_000;

    public string Kind => "ask_orb";

    public string DisplayName => "Ask another orb";

    public string Description =>
        "Ask one of the person's other orbs, a specialist, and read its answer. Use it when another orb knows the topic better. " +
        "Call it without \"orb\" to list the orbs and what each one does.";

    public string ParametersSchema => """
        {"type":"object","properties":{"orb":{"type":"string","description":"The name of the orb to ask."},"question":{"type":"string","description":"The question, with everything the other orb needs to know: it cannot see this chat."}}}
        """;

    public ToolRisk Risk => ToolRisk.Read;

    // It only reads the person's own orbs and their answers, and the specialist runs no tools.
    public bool AllowedByDefault => true;

    public async Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var others = (await orbs.ListAsync(cancellationToken)).Where(o => o.Id != invocation.OrbId).ToList();
        var name = ToolJson.String(invocation.Arguments, "orb");
        if (string.IsNullOrWhiteSpace(name))
        {
            return List(others);
        }

        var specialist = others.FirstOrDefault(o => string.Equals(o.Name, name.Trim().TrimStart('@'), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"There is no other orb named \"{name}\". {List(others)}");
        var question = ToolJson.String(invocation.Arguments, "question");
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Give the question to ask.");
        }

        return $"{specialist.Name} says: {await AnswerAsync(specialist, question, cancellationToken)}";
    }

    private async Task<string> AnswerAsync(Orb specialist, string question, CancellationToken cancellationToken)
    {
        ResolvedModel resolved;
        try
        {
            resolved = ModelResolver.Resolve(specialist, await providers.ListAsync(cancellationToken));
        }
        catch (ModelUnavailableException)
        {
            throw new InvalidOperationException($"{specialist.Name} has no model to answer with.");
        }

        var adapter = adapters.Get(resolved.Provider.Kind);
        var context = await contextSources.ListAsync(specialist.ContextSourceIds, cancellationToken);

        // A throwaway chat that is never saved: the specialist sees only the question.
        var chat = Conversation.Start(specialist.Id, clock.GetUtcNow());
        chat.AddUserMessage(question, clock.GetUtcNow());
        var turns = PromptBuilder.Build(
            specialist,
            context,
            chat.Messages,
            memories: await memories.ForPromptAsync(specialist, cancellationToken),
            retrieved: await retriever.RetrieveAsync(context, question, cancellationToken));

        var answer = new StringBuilder();
        await foreach (var output in adapter.StreamAsync(providerService.ConnectionFor(resolved.Provider), new ChatRequest(resolved.Model, turns), cancellationToken))
        {
            if (output is TextOutput text)
            {
                answer.Append(text.Text);
                if (answer.Length > MaxAnswerLength)
                {
                    break;
                }
            }
        }

        var result = answer.ToString().Trim();
        return result.Length == 0 ? "(no answer)"
            : result.Length <= MaxAnswerLength ? result
            : string.Concat(result.AsSpan(0, MaxAnswerLength - 3), "...");
    }

    private static string List(List<Orb> others)
    {
        if (others.Count == 0)
        {
            return "The person has no other orbs to ask.";
        }

        var list = new StringBuilder("Orbs you can ask:");
        foreach (var orb in others)
        {
            list.Append("\n- ").Append(orb.Name);
            var about = !string.IsNullOrWhiteSpace(orb.Description) ? orb.Description : orb.Instructions.Split('\n', 2)[0];
            if (!string.IsNullOrWhiteSpace(about))
            {
                list.Append(": ").Append(about.Length <= 160 ? about.Trim() : string.Concat(about.AsSpan(0, 157), "..."));
            }
        }

        return list.ToString();
    }
}
