using Microsoft.EntityFrameworkCore;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Jobs;
using Orbs.Application.Memories;
using Orbs.Application.Schedules;
using Orbs.Application.Tools;
using Orbs.Domain.Common;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Jobs;
using Orbs.Domain.Memories;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;
using Orbs.Domain.Schedules;
using Orbs.Domain.Tools;
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
    // Only the newest messages come along; long histories are paged with ListMessagesAsync.
    protected override IQueryable<Conversation> Query =>
        Db.Conversations.Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(Conversation.LoadedHistory));

    public async Task<IReadOnlyList<Conversation>> ListRecentAsync(Guid? orbId, int limit, CancellationToken cancellationToken = default)
    {
        var query = Db.Conversations.AsNoTracking();
        if (orbId is { } id)
        {
            query = query.Where(c => c.OrbId == id);
        }

        return await query.OrderByDescending(c => c.UpdatedAt).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> ListMessagesAsync(Guid conversationId, DateTimeOffset? before, int limit, CancellationToken cancellationToken = default)
    {
        var query = Db.Set<Message>().AsNoTracking().Where(m => EF.Property<Guid>(m, "ConversationId") == conversationId);
        if (before is { } cursor)
        {
            query = query.Where(m => m.CreatedAt < cursor);
        }

        return await query.OrderByDescending(m => m.CreatedAt).Take(limit).ToListAsync(cancellationToken);
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

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Db.Sessions.Where(s => s.ExpiresAt < now).ExecuteDeleteAsync(cancellationToken);
}

internal sealed class JobRepository(OrbsDbContext db) : EfRepository<Job>(db), IJobRepository
{
    public async Task<IReadOnlyList<Job>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default) =>
        await Query.Where(j => j.Status == JobStatus.Pending && j.RunAt <= now).OrderBy(j => j.RunAt).Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Job>> ListRunningAsync(CancellationToken cancellationToken = default) =>
        await Query.Where(j => j.Status == JobStatus.Running).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Job>> ListRecentAsync(int limit, CancellationToken cancellationToken = default) =>
        await Query.AsNoTracking().OrderByDescending(j => j.UpdatedAt).Take(limit).ToListAsync(cancellationToken);

    public Task<bool> AnyPendingAsync(string kind, CancellationToken cancellationToken = default) =>
        Query.AnyAsync(j => j.Kind == kind && j.Status == JobStatus.Pending, cancellationToken);
}

internal sealed class ToolApprovalRepository(OrbsDbContext db) : EfRepository<ToolApproval>(db), IToolApprovalRepository
{
    public async Task<IReadOnlyList<ToolApproval>> ListPendingAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        await Query.Where(a => a.ConversationId == conversationId && a.Status == ApprovalStatus.Pending).ToListAsync(cancellationToken);
}

internal sealed class McpServerRepository(OrbsDbContext db) : EfRepository<McpServer>(db), IMcpServerRepository;

internal sealed class IntegrationRepository(OrbsDbContext db) : EfRepository<Integration>(db), IIntegrationRepository;

internal sealed class MemoryRepository(OrbsDbContext db) : EfRepository<Memory>(db), IMemoryRepository
{
    public async Task<IReadOnlyList<Memory>> ListForOrbAsync(Guid orbId, CancellationToken cancellationToken = default) =>
        await Query.Where(m => m.OrbId == orbId || m.OrbId == null).OrderByDescending(m => m.UpdatedAt).ToListAsync(cancellationToken);
}

internal sealed class ContextChunkRepository(OrbsDbContext db) : EfRepository<ContextChunk>(db), IContextChunkRepository
{
    public async Task<IReadOnlyList<ContextChunk>> ListForSourcesAsync(IReadOnlyCollection<Guid> sourceIds, string model, CancellationToken cancellationToken = default)
    {
        var ids = sourceIds.ToList();
        return await Query.AsNoTracking().Where(c => ids.Contains(c.SourceId) && c.Model == model).ToListAsync(cancellationToken);
    }

    public Task DeleteForSourceAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        Query.Where(c => c.SourceId == sourceId).ExecuteDeleteAsync(cancellationToken);
}

internal sealed class ContextFileRepository(OrbsDbContext db) : EfRepository<ContextFile>(db), IContextFileRepository
{
    public Task<ContextFile?> FindBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        Query.FirstOrDefaultAsync(f => f.SourceId == sourceId, cancellationToken);

    public async Task<IReadOnlyList<ContextFile>> ListImagesAsync(IReadOnlyCollection<Guid> sourceIds, CancellationToken cancellationToken = default)
    {
        var ids = sourceIds.ToList();
        return await Query.AsNoTracking().Where(f => ids.Contains(f.SourceId) && f.ContentType.StartsWith("image/")).ToListAsync(cancellationToken);
    }
}

internal sealed class ScheduleRepository(OrbsDbContext db) : EfRepository<Schedule>(db), IScheduleRepository
{
    public async Task<IReadOnlyList<Schedule>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default) =>
        await Query.Where(s => s.IsEnabled && s.NextCheckAt != null && s.NextCheckAt <= now).OrderBy(s => s.NextCheckAt).Take(limit).ToListAsync(cancellationToken);
}
