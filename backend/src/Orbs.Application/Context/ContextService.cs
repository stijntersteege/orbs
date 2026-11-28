using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Context;

namespace Orbs.Application.Context;

/// <summary>Use cases for connecting context sources and keeping them in sync.</summary>
public sealed class ContextService(
    IContextSourceRepository sources,
    IOrbRepository orbs,
    PluginRegistry<IContextConnector> connectors,
    ContextIndexer indexer,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public IReadOnlyList<ContextKindDto> ListKinds() => [.. connectors.All.Select(c => c.ToDto())];

    public async Task<IReadOnlyList<ContextSourceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await sources.ListAsync(cancellationToken);
        return [.. all.OrderBy(s => s.CreatedAt).Select(s => s.ToDto())];
    }

    public async Task<ContextSourceDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToDto();

    /// <summary>Adds a source and reads it straight away, so orbs can use it at once.</summary>
    public async Task<ContextSourceDto> CreateAsync(SaveContextSourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var connector = connectors.Get(request.Kind);
        if (connector.Kind == FileContextService.Kind)
        {
            throw new DomainException("Upload files with POST /api/context-sources/files.");
        }

        var source = ContextSource.Create(request.Name, connector.Kind, clock.GetUtcNow());
        Configure(source, connector, request);
        sources.Add(source);
        await SyncAsync(source, connector, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await indexer.QueueAsync(source, cancellationToken);
        return source.ToDto();
    }

    public async Task<ContextSourceDto> UpdateAsync(Guid id, SaveContextSourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = await LoadAsync(id, cancellationToken);
        if (!string.Equals(source.Kind, request.Kind, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("A context source's kind cannot change. Add a new source instead.");
        }

        var connector = connectors.Get(source.Kind);
        source.Rename(request.Name, clock.GetUtcNow());
        if (connector.Kind == FileContextService.Kind)
        {
            // A file cannot be edited, only renamed.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return source.ToDto();
        }

        Configure(source, connector, request);
        await SyncAsync(source, connector, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await indexer.QueueAsync(source, cancellationToken);
        return source.ToDto();
    }

    public async Task<ContextSourceDto> SyncAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await LoadAsync(id, cancellationToken);
        await SyncAsync(source, connectors.Get(source.Kind), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await indexer.QueueAsync(source, cancellationToken);
        return source.ToDto();
    }

    /// <summary>Embeds the source again for retrieval, in the background.</summary>
    public async Task<bool> IndexAsync(Guid id, CancellationToken cancellationToken = default) =>
        await indexer.QueueAsync(await LoadAsync(id, cancellationToken), cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await LoadAsync(id, cancellationToken);
        foreach (var orb in await orbs.ListAsync(cancellationToken))
        {
            orb.UnlinkContext(source.Id, clock.GetUtcNow());
        }

        sources.Remove(source);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ContextSource> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await sources.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Context source", id);

    private void Configure(ContextSource source, IContextConnector connector, SaveContextSourceRequest request)
    {
        if (connector.UsesLocation && string.IsNullOrWhiteSpace(request.Location))
        {
            throw new DomainException($"{connector.DisplayName} needs a location.");
        }

        if (connector.UsesInput && string.IsNullOrWhiteSpace(request.Input))
        {
            throw new DomainException($"{connector.DisplayName} needs some content.");
        }

        source.Configure(
            connector.UsesLocation ? request.Location : null,
            connector.UsesInput ? request.Input : null,
            clock.GetUtcNow());

        if (request.SyncEvery is { } every)
        {
            source.KeepInSync(connector.UsesLocation ? every : SyncInterval.Off, clock.GetUtcNow());
        }
    }

    /// <summary>A failed read is recorded on the source instead of failing the request.</summary>
    private async Task SyncAsync(ContextSource source, IContextConnector connector, CancellationToken cancellationToken)
    {
        try
        {
            var content = await connector.ReadAsync(source, cancellationToken);
            source.MarkSynced(content, clock.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            source.MarkFailed(ex.Message, clock.GetUtcNow());
        }
    }
}

/// <summary>Finds sources whose background sync is due (as the system) and queues a sync for each, owned by its owner.</summary>
public sealed class ContextSyncDispatcher(IContextSourceRepository sources, Jobs.JobScheduler jobs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var due = await sources.ListDueForSyncAsync(now, 50, cancellationToken);
        foreach (var source in due)
        {
            source.SyncPlanned(now);
            await jobs.EnqueueAsync(ContextSyncJob.KindName, new ContextSyncJob.Payload(source.Id), maxAttempts: 2, ownerId: source.OwnerId, cancellationToken: cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}

/// <summary>Reads one source again in the background, as its owner, and re-indexes it when it changed.</summary>
public sealed class ContextSyncJob(ContextService context) : Jobs.IJobHandler
{
    public const string KindName = "context.sync";

    public string Kind => KindName;

    public sealed record Payload(Guid SourceId);

    public async Task<string?> HandleAsync(Domain.Jobs.Job job, CancellationToken cancellationToken)
    {
        var source = await context.SyncAsync(Jobs.JobScheduler.ReadPayload<Payload>(job).SourceId, cancellationToken);
        return source.Status == ContextSourceStatus.Failed
            ? throw new InvalidOperationException(source.LastError ?? "The sync failed.")
            : $"Synced {source.Name}: {source.ContentLength:N0} characters.";
    }
}
