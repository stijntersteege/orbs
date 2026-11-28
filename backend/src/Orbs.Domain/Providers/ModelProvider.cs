using Orbs.Domain.Common;

namespace Orbs.Domain.Providers;

/// <summary>
/// A connected source of models: a hosted API, a local runtime or any compatible endpoint.
/// <see cref="Kind"/> names the adapter that talks to it, so new kinds plug in without domain changes.
/// </summary>
public sealed class ModelProvider : OwnedAggregateRoot
{
    public const int MaxNameLength = 80;
    public const int MaxModelLength = 200;

    private ModelProvider(Guid id, string name, string kind, DateTimeOffset now) : base(id, now)
    {
        Name = name;
        Kind = kind;
    }

    // For EF Core materialization.
    private ModelProvider()
    {
        Name = string.Empty;
        Kind = string.Empty;
    }

    public string Name { get; private set; }

    /// <summary>Adapter key, for example <c>openai-compatible</c> or <c>echo</c>.</summary>
    public string Kind { get; private set; }

    public Uri? BaseUrl { get; private set; }

    /// <summary>The API key, encrypted at rest. Never leaves the server in plain text.</summary>
    public string? ProtectedApiKey { get; private set; }

    /// <summary>The model used when an orb is on Auto and this provider is the default.</summary>
    public string? DefaultModel { get; private set; }

    /// <summary>The model that turns context into embeddings for retrieval, or <c>null</c> for none.</summary>
    public string? EmbeddingModel { get; private set; }

    /// <summary>The model that turns speech into text, such as whisper-1. Null for none.</summary>
    public string? SpeechToTextModel { get; private set; }

    /// <summary>The model that reads replies aloud, such as tts-1 or kokoro. Null for none.</summary>
    public string? TextToSpeechModel { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public bool HasApiKey => ProtectedApiKey is not null;

    public static ModelProvider Create(string name, string kind, DateTimeOffset now, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guard.Text(name, nameof(Name), MaxNameLength), Slug.Normalize(kind, nameof(Kind)), now);

    public void Rename(string name, DateTimeOffset now)
    {
        Name = Guard.Text(name, nameof(Name), MaxNameLength);
        Touch(now);
    }

    public void PointAt(string? baseUrl, DateTimeOffset now)
    {
        BaseUrl = Guard.AbsoluteHttpUri(baseUrl, nameof(BaseUrl));
        Touch(now);
    }

    /// <summary>Stores an already-encrypted key; pass <c>null</c> to remove it.</summary>
    public void SetProtectedApiKey(string? protectedApiKey, DateTimeOffset now)
    {
        ProtectedApiKey = string.IsNullOrWhiteSpace(protectedApiKey) ? null : protectedApiKey;
        Touch(now);
    }

    public void SetDefaultModel(string? model, DateTimeOffset now)
    {
        var value = Guard.Text(model, nameof(DefaultModel), MaxModelLength, required: false);
        DefaultModel = value.Length == 0 ? null : value;
        Touch(now);
    }

    public void SetEmbeddingModel(string? model, DateTimeOffset now)
    {
        var value = Guard.Text(model, nameof(EmbeddingModel), MaxModelLength, required: false);
        EmbeddingModel = value.Length == 0 ? null : value;
        Touch(now);
    }

    public void SetSpeechModels(string? speechToText, string? textToSpeech, DateTimeOffset now)
    {
        var listen = Guard.Text(speechToText, nameof(SpeechToTextModel), MaxModelLength, required: false);
        var speak = Guard.Text(textToSpeech, nameof(TextToSpeechModel), MaxModelLength, required: false);
        SpeechToTextModel = listen.Length == 0 ? null : listen;
        TextToSpeechModel = speak.Length == 0 ? null : speak;
        Touch(now);
    }

    public void Enable(DateTimeOffset now)
    {
        IsEnabled = true;
        Touch(now);
    }

    public void Disable(DateTimeOffset now)
    {
        IsEnabled = false;
        Touch(now);
    }
}
