using Orbs.Application.Abstractions;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Jobs;
using Orbs.Application.Memories;
using Orbs.Domain.Common;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Jobs;
using Orbs.Domain.Memories;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Application.Tests.Fakes;

/// <summary>Plain in-memory repositories: simpler and more honest than mocking every call.</summary>
internal sealed class InMemoryStore : IUnitOfWork
{
    public InMemoryRepo<Orb> OrbItems { get; } = new();

    public InMemoryRepo<ModelProvider> ProviderItems { get; } = new();

    public InMemoryRepo<ContextSource> ContextItems { get; } = new();

    public InMemoryRepo<Conversation> ConversationItems { get; } = new();

    public InMemoryRepo<ToolApproval> ApprovalItems { get; } = new();

    public InMemoryRepo<Memory> MemoryItems { get; } = new();

    public InMemoryRepo<ContextChunk> ChunkItems { get; } = new();

    public InMemoryRepo<Job> JobItems { get; } = new();

    public InMemoryRepo<ContextFile> FileItems { get; } = new();

    public IContextFileRepository Files => new FileRepo(FileItems);

    public IContextChunkRepository Chunks => new ChunkRepo(ChunkItems);

    public IJobRepository Jobs => new JobRepo(JobItems);

    public IMemoryRepository Memories => new MemoryRepo(MemoryItems);

    public IToolApprovalRepository Approvals => new ApprovalRepo(ApprovalItems);

    public int Saves { get; private set; }

    public IOrbRepository Orbs => new OrbRepo(OrbItems);

    public IModelProviderRepository Providers => new ProviderRepo(ProviderItems);

    public IContextSourceRepository Context => new ContextRepo(ContextItems);

    public IConversationRepository Conversations => new ConversationRepo(ConversationItems);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }

    private sealed class OrbRepo(InMemoryRepo<Orb> inner) : Delegating<Orb>(inner), IOrbRepository;

    private sealed class ProviderRepo(InMemoryRepo<ModelProvider> inner) : Delegating<ModelProvider>(inner), IModelProviderRepository;

    private sealed class ContextRepo(InMemoryRepo<ContextSource> inner) : Delegating<ContextSource>(inner), IContextSourceRepository
    {
        public Task<IReadOnlyList<ContextSource>> ListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            var set = ids.ToHashSet();
            return Task.FromResult<IReadOnlyList<ContextSource>>([.. Inner.Items.Where(s => set.Contains(s.Id))]);
        }

        public Task<IReadOnlyList<ContextSource>> ListDueForSyncAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContextSource>>([.. Inner.Items.Where(s => s.NextSyncAt <= now).Take(limit)]);
    }

    private sealed class ChunkRepo(InMemoryRepo<ContextChunk> inner) : Delegating<ContextChunk>(inner), IContextChunkRepository
    {
        public Task<IReadOnlyList<ContextChunk>> ListForSourcesAsync(IReadOnlyCollection<Guid> sourceIds, string model, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContextChunk>>([.. Inner.Items.Where(c => sourceIds.Contains(c.SourceId) && c.Model == model)]);

        public Task DeleteForSourceAsync(Guid sourceId, CancellationToken cancellationToken = default)
        {
            Inner.Items.RemoveAll(c => c.SourceId == sourceId);
            return Task.CompletedTask;
        }
    }

    private sealed class FileRepo(InMemoryRepo<ContextFile> inner) : Delegating<ContextFile>(inner), IContextFileRepository
    {
        public Task<ContextFile?> FindBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Inner.Items.FirstOrDefault(f => f.SourceId == sourceId));

        public Task<IReadOnlyList<ContextFile>> ListImagesAsync(IReadOnlyCollection<Guid> sourceIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContextFile>>([.. Inner.Items.Where(f => sourceIds.Contains(f.SourceId) && f.IsImage)]);
    }

    private sealed class JobRepo(InMemoryRepo<Job> inner) : Delegating<Job>(inner), IJobRepository
    {
        public Task<IReadOnlyList<Job>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>([.. Inner.Items.Where(j => j.IsDue(now)).Take(limit)]);

        public Task<IReadOnlyList<Job>> ListRunningAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>([.. Inner.Items.Where(j => j.Status == JobStatus.Running)]);

        public Task<IReadOnlyList<Job>> ListRecentAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>([.. Inner.Items.Take(limit)]);

        public Task<bool> AnyPendingAsync(string kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(Inner.Items.Any(j => j.Kind == kind && j.Status == JobStatus.Pending));
    }

    private sealed class MemoryRepo(InMemoryRepo<Memory> inner) : Delegating<Memory>(inner), IMemoryRepository
    {
        public Task<IReadOnlyList<Memory>> ListForOrbAsync(Guid orbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Memory>>([.. Inner.Items.Where(m => m.OrbId == orbId || m.OrbId == null).OrderByDescending(m => m.UpdatedAt)]);
    }

    private sealed class ApprovalRepo(InMemoryRepo<ToolApproval> inner) : Delegating<ToolApproval>(inner), IToolApprovalRepository
    {
        public Task<IReadOnlyList<ToolApproval>> ListPendingAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolApproval>>([.. Inner.Items.Where(a => a.ConversationId == conversationId && a.Status == ApprovalStatus.Pending)]);
    }

    private sealed class ConversationRepo(InMemoryRepo<Conversation> inner) : Delegating<Conversation>(inner), IConversationRepository
    {
        public Task<IReadOnlyList<Conversation>> ListRecentAsync(Guid? orbId, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Conversation>>(
                [.. Inner.Items.Where(c => orbId is null || c.OrbId == orbId).OrderByDescending(c => c.UpdatedAt).Take(limit)]);

        public Task<IReadOnlyList<Message>> ListMessagesAsync(Guid conversationId, DateTimeOffset? before, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Message>>(
                [.. Inner.Items.Single(c => c.Id == conversationId).Messages
                    .Where(m => before is null || m.CreatedAt < before)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(limit)]);
    }

    private abstract class Delegating<T>(InMemoryRepo<T> inner) : IRepository<T> where T : AggregateRoot
    {
        protected InMemoryRepo<T> Inner { get; } = inner;

        public Task<T?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Inner.FindAsync(id, cancellationToken);

        public Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) => Inner.ListAsync(cancellationToken);

        public void Add(T aggregate) => Inner.Add(aggregate);

        public void Remove(T aggregate) => Inner.Remove(aggregate);
    }
}

internal sealed class InMemoryRepo<T> : IRepository<T> where T : AggregateRoot
{
    public List<T> Items { get; } = [];

    public Task<T?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<T>>([.. Items]);

    public void Add(T aggregate) => Items.Add(aggregate);

    public void Remove(T aggregate) => Items.Remove(aggregate);
}
