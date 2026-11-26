using Orbs.Domain.Context;

namespace Orbs.Application.Abstractions;

/// <summary>
/// Something that plugs into Orbs under a <see cref="Kind"/> key.
/// Adding a new provider or connector means adding a class, never editing a switch.
/// </summary>
public interface IPlugin
{
    /// <summary>Stable, lower-case key stored with the data, for example <c>openai-compatible</c>.</summary>
    string Kind { get; }

    string DisplayName { get; }

    string Description { get; }
}

// ---------- Models ----------

public enum ChatRole { System, User, Assistant }

public sealed record ChatTurn(ChatRole Role, string Content);

public sealed record ChatRequest(string Model, IReadOnlyList<ChatTurn> Messages);

/// <summary>What an adapter needs to reach a provider. The API key is already decrypted.</summary>
public sealed record ProviderConnection(Uri? BaseUrl, string? ApiKey);

public sealed record ModelInfo(string Id, string? DisplayName = null);

/// <summary>Talks to one kind of model provider (OpenAI-compatible, a local runtime, ...).</summary>
public interface IModelAdapter : IPlugin
{
    bool RequiresBaseUrl { get; }

    bool RequiresApiKey { get; }

    /// <summary>Pre-filled in the UI when adding a provider of this kind.</summary>
    Uri? DefaultBaseUrl { get; }

    /// <summary>Streams the reply as text fragments, in order.</summary>
    IAsyncEnumerable<string> StreamChatAsync(ProviderConnection connection, ChatRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default);
}

// ---------- Context ----------

/// <summary>Reads one kind of context source (a note, a web page, ...) into plain text for orbs.</summary>
public interface IContextConnector : IPlugin
{
    /// <summary>True when the source points somewhere, such as a URL.</summary>
    bool UsesLocation { get; }

    /// <summary>True when the user types or pastes the content itself.</summary>
    bool UsesInput { get; }

    Task<string> ReadAsync(ContextSource source, CancellationToken cancellationToken = default);
}

// ---------- Security ----------

/// <summary>Encrypts secrets such as API keys before they are stored.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
