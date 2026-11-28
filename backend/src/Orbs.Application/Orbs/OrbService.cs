using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Orbs;

/// <summary>Use cases for creating, shaping and removing orbs.</summary>
public sealed class OrbService(
    IOrbRepository orbs,
    IModelProviderRepository providers,
    IContextSourceRepository contextSources,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<OrbDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await orbs.ListAsync(cancellationToken);
        return [.. all.OrderBy(o => o.CreatedAt).Select(o => o.ToDto())];
    }

    public async Task<OrbDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToDto();

    public async Task<OrbDto> CreateAsync(SaveOrbRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orb = Orb.Create(request.Name, clock.GetUtcNow());
        await ApplyAsync(orb, request, cancellationToken);
        orbs.Add(orb);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return orb.ToDto();
    }

    public async Task<OrbDto> UpdateAsync(Guid id, SaveOrbRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orb = await LoadAsync(id, cancellationToken);
        orb.Rename(request.Name, clock.GetUtcNow());
        await ApplyAsync(orb, request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return orb.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orb = await LoadAsync(id, cancellationToken);
        orbs.Remove(orb);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Orb> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await orbs.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Orb", id);

    private async Task ApplyAsync(Orb orb, SaveOrbRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        orb.Describe(request.Description, now);
        orb.Brief(request.Instructions, now);
        orb.ChangeAppearance(request.Appearance ?? orb.Appearance, now);
        orb.UseVoice(request.Voice, now);
        orb.UseModel(await ResolveModelAsync(request.Model, cancellationToken), now);
        orb.SetContext(await ResolveContextAsync(request.ContextSourceIds, cancellationToken), now);

        // Omitted means "keep": older clients do not know about tool permissions.
        if (request.ToolPermissions is not null)
        {
            orb.SetToolPermissions(request.ToolPermissions, now);
        }
    }

    private async Task<ModelRef?> ResolveModelAsync(ModelRefDto? model, CancellationToken cancellationToken)
    {
        if (model is null)
        {
            return null;
        }

        _ = await providers.FindAsync(model.ProviderId, cancellationToken)
            ?? throw new DomainException($"Provider '{model.ProviderId}' does not exist.");
        return new ModelRef(model.ProviderId, model.Model);
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveContextAsync(IReadOnlyList<Guid>? ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        var wanted = ids.Distinct().ToList();
        var found = await contextSources.ListAsync(wanted, cancellationToken);
        var missing = wanted.Except(found.Select(s => s.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainException($"Context sources do not exist: {string.Join(", ", missing)}.");
        }

        return wanted;
    }
}
