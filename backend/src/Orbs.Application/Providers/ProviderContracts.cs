using Orbs.Application.Abstractions;
using Orbs.Domain.Providers;

namespace Orbs.Application.Providers;

public sealed record ProviderKindDto(
    string Kind,
    string DisplayName,
    string Description,
    bool RequiresBaseUrl,
    bool RequiresApiKey,
    string? DefaultBaseUrl,
    bool SupportsEmbeddings = false,
    bool SupportsSpeech = false);

/// <summary>A connected provider as the client sees it. The API key itself is never returned.</summary>
public sealed record ProviderDto(
    Guid Id,
    string Name,
    string Kind,
    string? BaseUrl,
    bool HasApiKey,
    string? DefaultModel,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? EmbeddingModel = null,
    bool SupportsEmbeddings = false,
    string? SpeechToTextModel = null,
    string? TextToSpeechModel = null);

/// <summary>
/// Creates or updates a provider. On update, a <c>null</c> <see cref="ApiKey"/> keeps the stored key
/// and an empty string removes it.
/// </summary>
public sealed record SaveProviderRequest(
    string Name,
    string Kind,
    string? BaseUrl = null,
    string? ApiKey = null,
    string? DefaultModel = null,
    bool IsEnabled = true,
    string? EmbeddingModel = null,
    string? SpeechToTextModel = null,
    string? TextToSpeechModel = null);

internal static class ProviderMapping
{
    public static ProviderDto ToDto(this ModelProvider p, bool supportsEmbeddings = false) => new(
        p.Id, p.Name, p.Kind, p.BaseUrl?.ToString(), p.HasApiKey, p.DefaultModel, p.IsEnabled, p.CreatedAt, p.UpdatedAt, p.EmbeddingModel, supportsEmbeddings, p.SpeechToTextModel, p.TextToSpeechModel);

    public static ProviderKindDto ToDto(this IModelAdapter a) => new(
        a.Kind, a.DisplayName, a.Description, a.RequiresBaseUrl, a.RequiresApiKey, a.DefaultBaseUrl?.ToString(), a is IEmbeddingAdapter, a is Voice.ISpeechToTextAdapter || a is Voice.ITextToSpeechAdapter);
}
