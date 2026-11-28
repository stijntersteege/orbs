using Microsoft.EntityFrameworkCore;
using Orbs.Application.Accounts;
using Orbs.Domain.Common;

namespace Orbs.Infrastructure.Persistence;

/// <summary>Bulk updates across every owned table, past the per-user filter.</summary>
internal sealed class OwnershipMaintenance(OrbsDbContext db) : IOwnershipMaintenance
{
    public async Task ClaimUnownedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (var set in OwnedSets())
        {
            await set.IgnoreQueryFilters().Where(e => e.OwnerId == Guid.Empty)
                .ExecuteUpdateAsync(u => u.SetProperty(e => e.OwnerId, userId), cancellationToken);
        }
    }

    public async Task DeleteOwnedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Chats first: they point at orbs.
        foreach (var set in OwnedSets())
        {
            await set.IgnoreQueryFilters().Where(e => e.OwnerId == userId).ExecuteDeleteAsync(cancellationToken);
        }
    }

    /// <summary>Every owned aggregate, children before parents. Later features add their sets here.</summary>
    private IEnumerable<IQueryable<OwnedAggregateRoot>> OwnedSets()
    {
        yield return db.Conversations;
        yield return db.Orbs;
        yield return db.ContextSources;
        yield return db.Providers;
    }
}
