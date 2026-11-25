using System.Runtime.CompilerServices;
using Orbs.Application.Abstractions;

namespace Orbs.Testing;

/// <summary>A provider that always breaks mid-reply, for testing error handling.</summary>
public sealed class FailingModelAdapter : IModelAdapter
{
    public const string KindName = "failing";

    public string Kind => KindName;

    public string DisplayName => "Failing (test)";

    public string Description => "Starts answering, then fails.";

    public bool RequiresBaseUrl => false;

    public bool RequiresApiKey => false;

    public Uri? DefaultBaseUrl => null;

    public async IAsyncEnumerable<string> StreamChatAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "Partial";
        await Task.Yield();
        throw new HttpRequestException("provider exploded");
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default) =>
        throw new HttpRequestException("provider exploded");
}
