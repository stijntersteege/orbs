using Orbs.Application.Abstractions;
using Orbs.Domain.Context;

namespace Orbs.Application.Context;

public sealed record ContextKindDto(string Kind, string DisplayName, string Description, bool UsesLocation, bool UsesInput);

public sealed record ContextSourceDto(
    Guid Id,
    string Name,
    string Kind,
    string Location,
    string Input,
    ContextSourceStatus Status,
    string? LastError,
    DateTimeOffset? LastSyncedAt,
    int ContentLength,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SaveContextSourceRequest(string Name, string Kind, string? Location = null, string? Input = null);

internal static class ContextMapping
{
    public static ContextSourceDto ToDto(this ContextSource s) => new(
        s.Id, s.Name, s.Kind, s.Location, s.Input, s.Status, s.LastError, s.LastSyncedAt, s.Content.Length, s.CreatedAt, s.UpdatedAt);

    public static ContextKindDto ToDto(this IContextConnector c) => new(
        c.Kind, c.DisplayName, c.Description, c.UsesLocation, c.UsesInput);
}
