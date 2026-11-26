using Microsoft.EntityFrameworkCore;
using Orbs.Application.Abstractions;
using Orbs.Domain.Common;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Infrastructure.Persistence.Repositories;

internal class EfRepository<T>(OrbsDbContext db) : IRepository<T> where T : AggregateRoot
{
    protected OrbsDbContext Db { get; } = db;

    protected virtual IQueryable<T> Query => Db.Set<T>();

    public virtual Task<T?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        await Query.ToListAsync(cancellationToken);

    public void Add(T aggregate) => Db.Set<T>().Add(aggregate);

    public void Remove(T aggregate) => Db.Set<T>().Remove(aggregate);
}

internal sealed class OrbRepository(OrbsDbContext db) : EfRepository<Orb>(db), IOrbRepository;

internal sealed class ModelProviderRepository(OrbsDbContext db) : EfRepository<ModelProvider>(db), IModelProviderRepository;

internal sealed class ContextSourceRepository(OrbsDbContext db) : EfRepository<ContextSource>(db), IContextSourceRepository
{
    public async Task<IReadOnlyList<ContextSource>> ListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids.ToList();
        return wanted.Count == 0 ? [] : await Query.Where(s => wanted.Contains(s.Id)).ToListAsync(cancellationToken);
    }
}

internal sealed class ConversationRepository(OrbsDbContext db) : EfRepository<Conversation>(db), IConversationRepository
{
    protected override IQueryable<Conversation> Query => Db.Conversations.Include(c => c.Messages);

    public async Task<IReadOnlyList<Conversation>> ListRecentAsync(Guid? orbId, int limit, CancellationToken cancellationToken = default)
    {
        var query = Db.Conversations.AsNoTracking();
        if (orbId is { } id)
        {
            query = query.Where(c => c.OrbId == id);
        }

        return await query.OrderByDescending(c => c.UpdatedAt).Take(limit).ToListAsync(cancellationToken);
    }
}
