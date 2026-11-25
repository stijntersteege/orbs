using System.Runtime.CompilerServices;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Models;

/// <summary>
/// A demo provider that answers by echoing the last user message, word by word.
/// Lets people try Orbs without an API key, and keeps tests independent of real models.
/// </summary>
internal sealed class EchoModelAdapter : IModelAdapter
{
    public string Kind => "echo";

    public string DisplayName => "Echo (demo)";

    public string Description => "Repeats what you say. No key needed, handy for trying Orbs out.";

    public bool RequiresBaseUrl => false;

    public bool RequiresApiKey => false;

    public Uri? DefaultBaseUrl => null;

    /// <summary>Pause between words so the UI shows real streaming. Zero in tests.</summary>
    internal TimeSpan WordDelay { get; init; } = TimeSpan.FromMilliseconds(25);

    public async IAsyncEnumerable<string> StreamChatAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? string.Empty;
        var words = $"You said: {lastUserMessage}".Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (WordDelay > TimeSpan.Zero)
            {
                await Task.Delay(WordDelay, cancellationToken);
            }

            yield return i == 0 ? words[i] : " " + words[i];
        }
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ModelInfo>>([new ModelInfo("echo-1", "Echo 1")]);
}
