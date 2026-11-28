using Microsoft.EntityFrameworkCore;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Domain.Common;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;
using Orbs.Domain.Users;

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

internal sealed class UserRepository(OrbsDbContext db) : EfRepository<User>(db), IUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Query.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByExternalLoginAsync(string issuer, string subject, CancellationToken cancellationToken = default) =>
        Query.FirstOrDefaultAsync(u => u.ExternalLogins.Any(l => l.Issuer == issuer && l.Subject == subject), cancellationToken);

    public Task<User?> FindByPasskeyAsync(byte[] credentialId, CancellationToken cancellationToken = default) =>
        Query.FirstOrDefaultAsync(u => u.Passkeys.Any(p => p.CredentialId == credentialId), cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Db.Users.AnyAsync(cancellationToken);
}

internal sealed class SessionRepository(OrbsDbContext db) : EfRepository<Session>(db), ISessionRepository
{
    public Task<Session?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Query.FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<Session>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await Query.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
}
