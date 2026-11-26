using System.Runtime.CompilerServices;
using Orbs.Application.Abstractions;
using Orbs.Domain.Context;

namespace Orbs.Application.Tests.Fakes;

internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public static FakeClock Default => new(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Reversible "encryption" so tests can see that keys were protected.</summary>
internal sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => "protected:" + plaintext;

    public string Unprotect(string protectedValue) => protectedValue["protected:".Length..];
}

/// <summary>A model adapter that replies with fixed fragments and records what it was asked.</summary>
internal sealed class ScriptedModelAdapter(string kind = "scripted", params string[] fragments) : IModelAdapter
{
    public string Kind => kind;

    public string DisplayName => "Scripted";

    public string Description => "Replies with a fixed script.";

    public bool RequiresBaseUrl { get; init; }

    public bool RequiresApiKey { get; init; }

    public Uri? DefaultBaseUrl { get; init; }

    public Exception? FailAfterFirstFragment { get; init; }

    public ChatRequest? LastRequest { get; private set; }

    public ProviderConnection? LastConnection { get; private set; }

    public async IAsyncEnumerable<string> StreamChatAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        LastConnection = connection;
        for (var i = 0; i < fragments.Length; i++)
        {
            await Task.Yield();
            yield return fragments[i];
            if (FailAfterFirstFragment is not null)
            {
                throw FailAfterFirstFragment;
            }
        }
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ModelInfo>>([new ModelInfo("model-a")]);
}

internal sealed class StubConnector(string kind, Func<ContextSource, string> read, bool usesLocation = false, bool usesInput = true) : IContextConnector
{
    public string Kind => kind;

    public string DisplayName => kind;

    public string Description => "Stub";

    public bool UsesLocation => usesLocation;

    public bool UsesInput => usesInput;

    public Task<string> ReadAsync(ContextSource source, CancellationToken cancellationToken = default) =>
        Task.FromResult(read(source));
}
