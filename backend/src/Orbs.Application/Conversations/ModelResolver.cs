using Orbs.Application.Common;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Application.Conversations;

public sealed record ResolvedModel(ModelProvider Provider, string Model);

/// <summary>
/// Decides which model answers for an orb.
/// A pinned model wins while its provider is enabled; otherwise "Auto" picks the
/// oldest enabled provider that has a default model.
/// </summary>
public static class ModelResolver
{
    public static ResolvedModel Resolve(Orb orb, IReadOnlyList<ModelProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(orb);
        ArgumentNullException.ThrowIfNull(providers);

        if (orb.Model is { } pinned)
        {
            var provider = providers.FirstOrDefault(p => p.Id == pinned.ProviderId && p.IsEnabled);
            if (provider is not null)
            {
                return new ResolvedModel(provider, pinned.Model);
            }
        }

        var fallback = providers
            .Where(p => p.IsEnabled && p.DefaultModel is not null)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault();

        return fallback is null
            ? throw new ModelUnavailableException()
            : new ResolvedModel(fallback, fallback.DefaultModel!);
    }
}
