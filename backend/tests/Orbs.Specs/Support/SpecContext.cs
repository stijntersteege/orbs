using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Testing;

namespace Orbs.Specs.Support;

/// <summary>
/// Per-scenario state: a fresh in-memory Orbs server plus what the scenario created.
/// Reqnroll injects one instance into every step class of a scenario and disposes it afterwards.
/// <see cref="Api"/> is signed in as the server's first account, created on first use.
/// </summary>
public sealed class SpecContext : IDisposable
{
    private OrbsApiFactory _factory = new();
    private ApiClient? _api;

    public OrbsApiFactory Factory => _factory;

    public ApiClient Api => _api ??= new ApiClient(_factory.CreateClient());

    /// <summary>Other signed-in accounts, by email.</summary>
    public Dictionary<string, ApiClient> Users { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, OrbDto> Orbs { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ContextSourceDto> Sources { get; } = new(StringComparer.Ordinal);

    public HttpResponseMessage? LastResponse { get; set; }

    public SendResult? LastReply { get; set; }

    public ConversationDto? Conversation { get; set; }

    /// <summary>The oldest message time on the last page shown, for paging back.</summary>
    public DateTimeOffset? LastPageStart { get; set; }

    /// <summary>Swaps in a differently configured server. Call before anything uses <see cref="Api"/>.</summary>
    public void UseServer(OrbsApiFactory factory)
    {
        _factory.Dispose();
        _factory = factory;
        _api = null;
    }

    public void SignInAsOwner() => _ = Api;

    public void Dispose() => _factory.Dispose();
}
