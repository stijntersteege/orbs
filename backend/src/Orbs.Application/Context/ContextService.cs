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
        var source = ContextSource.Create(request.Name, connector.Kind, clock.GetUtcNow());
        Configure(source, connector, request);
        sources.Add(source);
        await SyncAsync(source, connector, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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
        Configure(source, connector, request);
        await SyncAsync(source, connector, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return source.ToDto();
    }

    public async Task<ContextSourceDto> SyncAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await LoadAsync(id, cancellationToken);
        await SyncAsync(source, connectors.Get(source.Kind), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return source.ToDto();
    }

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
