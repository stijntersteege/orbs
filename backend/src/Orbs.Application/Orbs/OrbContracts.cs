using Orbs.Domain.Orbs;

namespace Orbs.Application.Orbs;

public sealed record ModelRefDto(Guid ProviderId, string Model);

public sealed record OrbDto(
    Guid Id,
    string Name,
    string Description,
    string Instructions,
    OrbAppearance Appearance,
    ModelRefDto? Model,
    IReadOnlyList<Guid> ContextSourceIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Creates or fully updates an orb. Omitted optional parts fall back to defaults.</summary>
public sealed record SaveOrbRequest(
    string Name,
    string? Description = null,
    string? Instructions = null,
    OrbAppearance? Appearance = null,
    ModelRefDto? Model = null,
    IReadOnlyList<Guid>? ContextSourceIds = null);

internal static class OrbMapping
{
    public static OrbDto ToDto(this Orb orb) => new(
        orb.Id,
        orb.Name,
        orb.Description,
        orb.Instructions,
        orb.Appearance,
        orb.Model is null ? null : new ModelRefDto(orb.Model.ProviderId, orb.Model.Model),
        [.. orb.ContextSourceIds],
        orb.CreatedAt,
        orb.UpdatedAt);
}
