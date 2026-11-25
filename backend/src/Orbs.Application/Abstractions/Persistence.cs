using Orbs.Domain.Common;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Application.Abstractions;

/// <summary>Loads and stores one kind of aggregate. Changes are written by <see cref="IUnitOfWork"/>.</summary>
public interface IRepository<T> where T : AggregateRoot
{
    Task<T?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    void Add(T aggregate);

    void Remove(T aggregate);
}

public interface IOrbRepository : IRepository<Orb>;

public interface IModelProviderRepository : IRepository<ModelProvider>;

public interface IContextSourceRepository : IRepository<ContextSource>
{
    Task<IReadOnlyList<ContextSource>> ListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}

public interface IConversationRepository : IRepository<Conversation>
{
    /// <summary>Most recently updated first, without messages.</summary>
    Task<IReadOnlyList<Conversation>> ListRecentAsync(Guid? orbId, int limit, CancellationToken cancellationToken = default);
}

/// <summary>Commits every change made through the repositories as one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
