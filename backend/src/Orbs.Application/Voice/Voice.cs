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

/// <summary>Text to read aloud, in a given voice or in an orb's voice.</summary>
public sealed record SpeakRequest(string Text, string? Voice = null, Guid? OrbId = null);

/// <summary>What the app needs to offer voice: whether listening and speaking work, and which voices to suggest.</summary>
public sealed record VoiceStatusDto(bool CanListen, bool CanSpeak, IReadOnlyList<string> Voices);

/// <summary>
/// Voice in and out, through whichever provider has a speech model set: like "Auto", the first enabled one.
/// Works with OpenAI and anything that speaks its audio API: a local Whisper server, Kokoro, Piper.
/// </summary>
public sealed class VoiceService(IModelProviderRepository providers, IOrbRepository orbs, PluginRegistry<IModelAdapter> adapters, ProviderService providerService)
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
            var text = await adapter.TranscribeAsync(providerService.ConnectionFor(provider), provider.SpeechToTextModel!, audio, contentType ?? "audio/webm", language, cancellationToken);
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
        var orbVoice = request.OrbId is { } orbId ? (await orbs.FindAsync(orbId, cancellationToken))?.Voice : null;
        var voice = !string.IsNullOrWhiteSpace(request.Voice) ? request.Voice.Trim() : orbVoice ?? (adapter.SuggestedVoices.Count > 0 ? adapter.SuggestedVoices[0] : DefaultVoice);
        try
        {
            return await adapter.SpeakAsync(providerService.ConnectionFor(provider), provider.TextToSpeechModel!, voice, text, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException($"Could not speak: {ex.Message}", ex);
        }
    }

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
