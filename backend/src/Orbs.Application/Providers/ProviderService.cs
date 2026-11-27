using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Providers;

namespace Orbs.Application.Providers;

/// <summary>Use cases for connecting model providers and browsing their models.</summary>
public sealed class ProviderService(
    IModelProviderRepository providers,
    IOrbRepository orbs,
    PluginRegistry<IModelAdapter> adapters,
    ISecretProtector secrets,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    Accounts.CurrentUser currentUser)
{
    public IReadOnlyList<ProviderKindDto> ListKinds() => [.. adapters.All.Select(a => a.ToDto())];

    public async Task<IReadOnlyList<ProviderDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await providers.ListAsync(cancellationToken);
        return [.. all.OrderBy(p => p.CreatedAt).Select(p => p.ToDto())];
    }

    public async Task<ProviderDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToDto();

    public async Task<ProviderDto> CreateAsync(SaveProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var adapter = adapters.Get(request.Kind);
        var provider = ModelProvider.Create(request.Name, adapter.Kind, clock.GetUtcNow());
        Apply(provider, adapter, request);
        providers.Add(provider);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return provider.ToDto();
    }

    public async Task<ProviderDto> UpdateAsync(Guid id, SaveProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = await LoadAsync(id, cancellationToken);
        if (!string.Equals(provider.Kind, request.Kind, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("A provider's kind cannot change. Add a new provider instead.");
        }

        provider.Rename(request.Name, clock.GetUtcNow());
        Apply(provider, adapters.Get(provider.Kind), request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return provider.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await LoadAsync(id, cancellationToken);
        foreach (var orb in (await orbs.ListAsync(cancellationToken)).Where(o => o.Model?.ProviderId == provider.Id))
        {
            orb.UseModel(null, clock.GetUtcNow());
        }

        providers.Remove(provider);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await LoadAsync(id, cancellationToken);
        var adapter = adapters.Get(provider.Kind);
        try
        {
            return await adapter.ListModelsAsync(ConnectionFor(provider), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ExternalServiceException($"Could not reach {provider.Name}: {ex.Message}", ex);
        }
    }

    /// <summary>Builds what an adapter needs to call this provider, decrypting the key.</summary>
    public ProviderConnection ConnectionFor(ModelProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var apiKey = provider.ProtectedApiKey is null ? null : secrets.Unprotect(provider.ProtectedApiKey);
        return new ProviderConnection(provider.BaseUrl, apiKey, RestrictNetwork: !currentUser.IsAdmin);
    }

    private async Task<ModelProvider> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await providers.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Provider", id);

    private void Apply(ModelProvider provider, IModelAdapter adapter, SaveProviderRequest request)
    {
        var now = clock.GetUtcNow();
        provider.PointAt(string.IsNullOrWhiteSpace(request.BaseUrl) ? adapter.DefaultBaseUrl?.ToString() : request.BaseUrl, now);
        provider.SetDefaultModel(request.DefaultModel, now);
        provider.SetEmbeddingModel(adapter is IEmbeddingAdapter ? request.EmbeddingModel : null, now);
        provider.SetSpeechModels(
            adapter is Voice.ISpeechToTextAdapter ? request.SpeechToTextModel : null,
            adapter is Voice.ITextToSpeechAdapter ? request.TextToSpeechModel : null,
            now);

        if (request.ApiKey is not null)
        {
            provider.SetProtectedApiKey(request.ApiKey.Length == 0 ? null : secrets.Protect(request.ApiKey.Trim()), now);
        }

        if (request.IsEnabled)
        {
            provider.Enable(now);
        }
        else
        {
            provider.Disable(now);
        }

        if (adapter.RequiresBaseUrl && provider.BaseUrl is null)
        {
            throw new DomainException($"{adapter.DisplayName} needs a base URL.");
        }

        if (adapter.RequiresApiKey && !provider.HasApiKey)
        {
            throw new DomainException($"{adapter.DisplayName} needs an API key.");
        }
    }
}
