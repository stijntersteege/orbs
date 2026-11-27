using System.Runtime.CompilerServices;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Models;

/// <summary>
/// A demo provider that answers by echoing the last user message, word by word.
/// Lets people try Orbs without an API key, and keeps tests independent of real models.
/// It can also try tools: "/tool current_time" or "/tool calculator {"expression":"2+2"}" calls that tool
/// and then repeats what it said.
/// </summary>
internal sealed class EchoModelAdapter : IModelAdapter
{
    public const string ToolCommand = "/tool ";

    public string Kind => "echo";

    public string DisplayName => "Echo (demo)";

    public string Description => "Repeats what you say. No key needed, handy for trying Orbs out. Type \"/tool <name>\" to try a tool.";

    public bool RequiresBaseUrl => false;

    public bool RequiresApiKey => false;

    public Uri? DefaultBaseUrl => null;

    public bool SupportsTools => true;

    /// <summary>Pause between words so the UI shows real streaming. Zero in tests.</summary>
    internal TimeSpan WordDelay { get; init; } = TimeSpan.FromMilliseconds(25);

    public async IAsyncEnumerable<string> StreamChatAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var output in StreamAsync(connection, request, cancellationToken))
        {
            if (output is TextOutput text)
            {
                yield return text.Text;
            }
        }
    }

    public async IAsyncEnumerable<ModelOutput> StreamAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var last = request.Messages.Count > 0 ? request.Messages[^1] : null;

        // After a tool ran: repeat what it said.
        if (last?.Role == ChatRole.Tool)
        {
            var call = request.Messages.SelectMany(m => m.ToolCalls ?? []).LastOrDefault(c => c.Id == last.ToolCallId);
            await foreach (var word in Words($"{call?.Name ?? "The tool"} said: {last.Content}", cancellationToken))
            {
                yield return new TextOutput(word);
            }

            yield break;
        }

        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? string.Empty;
        if (lastUserMessage.StartsWith(ToolCommand, StringComparison.Ordinal) && last?.Role == ChatRole.User)
        {
            var rest = lastUserMessage[ToolCommand.Length..].Trim();
            var space = rest.IndexOf(' ', StringComparison.Ordinal);
            var name = space < 0 ? rest : rest[..space];
            var arguments = space < 0 ? "{}" : rest[(space + 1)..].Trim();
            if (request.Tools?.Any(t => t.Name == name) == true)
            {
                yield return new ToolCallOutput(new ToolCall($"call_{Guid.NewGuid():N}", name, arguments));
                yield break;
            }
        }

        await foreach (var word in Words($"You said: {lastUserMessage}", cancellationToken))
        {
            yield return new TextOutput(word);
        }
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ModelInfo>>([new ModelInfo("echo-1", "Echo 1")]);

    private async IAsyncEnumerable<string> Words(string text, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (WordDelay > TimeSpan.Zero)
            {
                await Task.Delay(WordDelay, cancellationToken);
            }

            yield return i == 0 ? words[i] : " " + words[i];
        }
    }
}
