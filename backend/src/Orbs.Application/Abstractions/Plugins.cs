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

public enum ChatRole { System, User, Assistant, Tool }

/// <summary>
/// One message sent to a model. An assistant turn may carry the tools it asked for;
/// a tool turn carries one tool's result and the id of the call it answers.
/// </summary>
public sealed record ChatTurn(ChatRole Role, string Content, IReadOnlyList<ToolCall>? ToolCalls = null, string? ToolCallId = null);

/// <summary>What to send to a model. <see cref="Tools"/> lists the tools it may call; empty means none.</summary>
public sealed record ChatRequest(string Model, IReadOnlyList<ChatTurn> Messages, IReadOnlyList<ToolDefinition>? Tools = null);

/// <summary>A tool as a model sees it: a name, what it does and a JSON Schema for its arguments.</summary>
public sealed record ToolDefinition(string Name, string Description, string ParametersSchema);

/// <summary>A model asking to run a tool. <see cref="Arguments"/> is a JSON object.</summary>
public sealed record ToolCall(string Id, string Name, string Arguments);

/// <summary>Something a model produced while streaming: text, or a request to call a tool.</summary>
public abstract record ModelOutput;

public sealed record TextOutput(string Text) : ModelOutput;

public sealed record ToolCallOutput(ToolCall Call) : ModelOutput;

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

    /// <summary>True when the adapter passes <see cref="ChatRequest.Tools"/> to the model and reports tool calls.</summary>
    bool SupportsTools => false;

    /// <summary>
    /// Streams the reply as text and tool calls. Adapters without tool support get this for free:
    /// it wraps <see cref="StreamChatAsync"/>.
    /// </summary>
    async IAsyncEnumerable<ModelOutput> StreamAsync(
        ProviderConnection connection,
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var text in StreamChatAsync(connection, request, cancellationToken))
        {
            yield return new TextOutput(text);
        }
    }
}

// ---------- Tools ----------

/// <summary>How careful Orbs is with a tool by default. Per-orb permissions can override it.</summary>
public enum ToolRisk
{
    /// <summary>Only reads, such as the time or a search.</summary>
    Read,

    /// <summary>Changes something or reaches out, such as sending email.</summary>
    Write,
}

/// <summary>Who is calling a tool, so it can act for the right user and orb.</summary>
public sealed record ToolInvocation(Guid OrbId, Guid? UserId, string Arguments);

/// <summary>
/// Something an orb can do: look something up, or act in another app.
/// <see cref="IPlugin.Kind"/> is the tool's name as models see it (letters, digits, '_' and '-').
/// </summary>
public interface ITool : IPlugin
{
    /// <summary>JSON Schema of the arguments object.</summary>
    string ParametersSchema { get; }

    ToolRisk Risk { get; }

    /// <summary>
    /// True for tools that only touch the orb's own, visible state (such as its memory): they run without asking
    /// unless the person changes the orb's setting. Everything else asks first.
    /// </summary>
    bool AllowedByDefault => false;

    /// <summary>Runs the tool and returns what the model should read. Throw to report a failure to the model.</summary>
    Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Supplies tools for the current user. Built-in tools come from one source; each connected
/// MCP server or integration can be another.
/// </summary>
public interface IToolSource
{
    Task<IReadOnlyList<ITool>> ListAsync(CancellationToken cancellationToken = default);
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
