using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Application.Providers;
using Orbs.Domain.Common;
using Orbs.Domain.Providers;

namespace Orbs.Application.Voice;

/// <summary>A model adapter that can turn speech into text.</summary>
public interface ISpeechToTextAdapter
{
    Task<string> TranscribeAsync(ProviderConnection connection, string model, byte[] audio, string contentType, string? language, CancellationToken cancellationToken = default);
}

/// <summary>A model adapter that can read text aloud.</summary>
public interface ITextToSpeechAdapter
{
    /// <summary>Voices to offer in the face editor. Empty when the provider has no fixed list.</summary>
    IReadOnlyList<string> SuggestedVoices { get; }

    Task<SpokenAudio> SpeakAsync(ProviderConnection connection, string model, string voice, string text, CancellationToken cancellationToken = default);
}

public sealed record SpokenAudio(byte[] Data, string ContentType);

public sealed record TranscriptionDto(string Text);

public sealed record SpeakRequest(string Text, string? Voice = null);

/// <summary>What the app needs to offer voice: whether listening and speaking work, and which voices to suggest.</summary>
public sealed record VoiceStatusDto(bool CanListen, bool CanSpeak, IReadOnlyList<string> Voices);

/// <summary>
/// Voice in and out, through whichever provider has a speech model set: like "Auto", the first enabled one.
/// Works with OpenAI and anything that speaks its audio API: a local Whisper server, Kokoro, Piper.
/// </summary>
public sealed class VoiceService(IModelProviderRepository providers, PluginRegistry<IModelAdapter> adapters, ProviderService providerService)
{
    public const int MaxAudioBytes = 25 * 1024 * 1024;
    public const int MaxSpeechCharacters = 4_000;
    public const string DefaultVoice = "alloy";

    public async Task<VoiceStatusDto> StatusAsync(CancellationToken cancellationToken = default)
    {
        var listener = await FindAsync<ISpeechToTextAdapter>(p => p.SpeechToTextModel, cancellationToken);
        var speaker = await FindAsync<ITextToSpeechAdapter>(p => p.TextToSpeechModel, cancellationToken);
        return new VoiceStatusDto(listener is not null, speaker is not null, speaker?.Adapter.SuggestedVoices ?? []);
    }

    public async Task<TranscriptionDto> TranscribeAsync(byte[] audio, string? contentType, string? language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        if (audio.Length is 0 or > MaxAudioBytes)
        {
            throw new DomainException("Recordings can be at most 25 MB.");
        }

        var (provider, adapter) = await FindAsync<ISpeechToTextAdapter>(p => p.SpeechToTextModel, cancellationToken)
            ?? throw new ModelUnavailableException("No provider has a speech-to-text model. Set one under Models.");
        try
        {
            var text = await adapter.TranscribeAsync(providerService.ConnectionFor(provider), provider.SpeechToTextModel!, audio, RecordingType(contentType), language, cancellationToken);
            return new TranscriptionDto(text.Trim());
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException($"Could not transcribe: {ex.Message}", ex);
        }
    }

    public async Task<SpokenAudio> SpeakAsync(SpeakRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = Guard.Text(request.Text, nameof(request.Text), int.MaxValue);
        if (text.Length > MaxSpeechCharacters)
        {
            text = text[..MaxSpeechCharacters];
        }

        var (provider, adapter) = await FindAsync<ITextToSpeechAdapter>(p => p.TextToSpeechModel, cancellationToken)
            ?? throw new ModelUnavailableException("No provider has a text-to-speech model. Set one under Models.");
        try
        {
            var spoken = await adapter.SpeakAsync(providerService.ConnectionFor(provider), provider.TextToSpeechModel!, string.IsNullOrWhiteSpace(request.Voice) ? DefaultVoice : request.Voice.Trim(), text, cancellationToken);
            return spoken with { ContentType = SpokenType(spoken.ContentType) };
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException($"Could not speak: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The audio type of a recording, from the known ones only, without parameters such as ";codecs=opus".
    /// Anything else (a browser that sends nothing, or something that is not audio) counts as WebM.
    /// </summary>
    public static string RecordingType(string? contentType) =>
        KnownAudioTypes.TryGetValue(BareType(contentType), out var known) ? known : "audio/webm";

    /// <summary>
    /// The type Orbs serves spoken audio as. Only known audio types pass, so a provider can never make the app
    /// serve HTML or script from its own address.
    /// </summary>
    public static string SpokenType(string? contentType) =>
        KnownAudioTypes.TryGetValue(BareType(contentType), out var known) ? known : "audio/mpeg";

    private static readonly Dictionary<string, string> KnownAudioTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/webm"] = "audio/webm",
        ["audio/ogg"] = "audio/ogg",
        ["audio/opus"] = "audio/opus",
        ["audio/mp4"] = "audio/mp4",
        ["audio/m4a"] = "audio/mp4",
        ["audio/x-m4a"] = "audio/mp4",
        ["audio/aac"] = "audio/aac",
        ["audio/mpeg"] = "audio/mpeg",
        ["audio/mp3"] = "audio/mpeg",
        ["audio/wav"] = "audio/wav",
        ["audio/wave"] = "audio/wav",
        ["audio/x-wav"] = "audio/wav",
        ["audio/flac"] = "audio/flac",
        ["audio/pcm"] = "audio/pcm",
    };

    private static string BareType(string? contentType) =>
        (contentType ?? string.Empty).Split(';', 2)[0].Trim();

    private async Task<(ModelProvider Provider, T Adapter)?> FindAsync<T>(Func<ModelProvider, string?> model, CancellationToken cancellationToken)
        where T : class
    {
        var provider = (await providers.ListAsync(cancellationToken))
            .Where(p => p.IsEnabled && model(p) is not null && adapters.Contains(p.Kind) && adapters.Get(p.Kind) is T)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault();
        return provider is null ? null : (provider, (T)(object)adapters.Get(provider.Kind));
    }
}
