using System.Text;
using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Application.Jobs;
using Orbs.Application.Providers;
using Orbs.Domain.Context;
using Orbs.Domain.Jobs;
using Orbs.Domain.Providers;

namespace Orbs.Application.Context;

public interface IContextChunkRepository : IRepository<ContextChunk>
{
    Task<IReadOnlyList<ContextChunk>> ListForSourcesAsync(IReadOnlyCollection<Guid> sourceIds, string model, CancellationToken cancellationToken = default);

    Task DeleteForSourceAsync(Guid sourceId, CancellationToken cancellationToken = default);
}

/// <summary>A provider that can embed text, picked like "Auto": the first enabled provider with an embedding model.</summary>
public sealed record Embedder(IEmbeddingAdapter Adapter, ProviderConnection Connection, string Model)
{
    public async Task<float[]> EmbedOneAsync(string text, CancellationToken cancellationToken) =>
        (await Adapter.EmbedAsync(Connection, Model, [text], cancellationToken))[0];
}

/// <summary>Finds the current user's embedder, if any.</summary>
public sealed class EmbedderResolver(IModelProviderRepository providers, PluginRegistry<IModelAdapter> adapters, ProviderService providerService)
{
    public async Task<Embedder?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var candidate = (await providers.ListAsync(cancellationToken))
            .Where(p => p.IsEnabled && p.EmbeddingModel is not null && adapters.Contains(p.Kind) && adapters.Get(p.Kind) is IEmbeddingAdapter)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault();
        return candidate is null
            ? null
            : new Embedder((IEmbeddingAdapter)adapters.Get(candidate.Kind), providerService.ConnectionFor(candidate), candidate.EmbeddingModel!);
    }
}

/// <summary>Splits text into overlapping passages, preferring paragraph and sentence boundaries.</summary>
public static class Chunker
{
    public const int Size = 1_000;
    public const int Overlap = 150;

    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var chunks = new List<string>();
        var start = 0;
        while (start < clean.Length)
        {
            var end = Math.Min(start + Size, clean.Length);
            if (end < clean.Length)
            {
                var window = clean.AsSpan(start, end - start);
                var cut = window.LastIndexOf("\n\n");
                if (cut < Size / 2)
                {
                    cut = window.LastIndexOf(". ");
                }

                if (cut >= Size / 2)
                {
                    end = start + cut + 1;
                }
            }

            var chunk = clean[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (end >= clean.Length)
            {
                break;
            }

            start = Math.Max(end - Overlap, start + 1);
        }

        return chunks;
    }

    public static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0;
        }

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }
}

/// <summary>Re-embeds one context source in the background after it changed.</summary>
public sealed class ContextIndexJob(
    IContextSourceRepository sources,
    IContextChunkRepository chunks,
    EmbedderResolver embedders,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IJobHandler
{
    public const string KindName = "context.index";
    private const int BatchSize = 32;

    public string Kind => KindName;

    public sealed record Payload(Guid SourceId);

    public async Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobScheduler.ReadPayload<Payload>(job);
        var source = await sources.FindAsync(payload.SourceId, cancellationToken);
        if (source is null || source.Status != ContextSourceStatus.Ready)
        {
            return "The source is gone or not ready; nothing to index.";
        }

        var embedder = await embedders.ResolveAsync(cancellationToken)
            ?? throw new InvalidOperationException("No provider has an embedding model. Set one under Models.");

        var passages = Chunker.Split(source.Content);
        var vectors = new List<float[]>(passages.Count);
        foreach (var batch in passages.Chunk(BatchSize))
        {
            vectors.AddRange(await embedder.Adapter.EmbedAsync(embedder.Connection, embedder.Model, batch, cancellationToken));
        }

        await chunks.DeleteForSourceAsync(source.Id, cancellationToken);
        var now = clock.GetUtcNow();
        for (var i = 0; i < passages.Count; i++)
        {
            chunks.Add(ContextChunk.Create(source.Id, i, passages[i], vectors[i], embedder.Model, now));
        }

        source.MarkIndexed(passages.Count, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return $"Indexed {source.Name}: {passages.Count} passages with {embedder.Model}.";
    }
}

/// <summary>Queues re-indexing when a source changes and an embedder is available.</summary>
public sealed class ContextIndexer(EmbedderResolver embedders, JobScheduler jobs, Accounts.CurrentUser currentUser)
{
    public async Task<bool> QueueAsync(ContextSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Status != ContextSourceStatus.Ready || await embedders.ResolveAsync(cancellationToken) is null)
        {
            return false;
        }

        await jobs.EnqueueAsync(ContextIndexJob.KindName, new ContextIndexJob.Payload(source.Id), maxAttempts: 3, ownerId: currentUser.UserId, cancellationToken: cancellationToken);
        return true;
    }
}

/// <summary>The passages of indexed sources closest to a question, per source.</summary>
public sealed class ContextRetriever(IContextChunkRepository chunks, EmbedderResolver embedders)
{
    public const int TopPassages = 8;
    public const int MaxCharacters = 12_000;
    public const float RelativeCutoff = 0.5f;

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> RetrieveAsync(IReadOnlyList<ContextSource> sources, string question, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var indexed = sources.Where(s => s.Status == ContextSourceStatus.Ready && s.IndexedChunks > 0).Select(s => s.Id).ToList();
        if (indexed.Count == 0 || string.IsNullOrWhiteSpace(question))
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var embedder = await embedders.ResolveAsync(cancellationToken);
        if (embedder is null)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        try
        {
            var query = await embedder.EmbedOneAsync(question, cancellationToken);
            var candidates = await chunks.ListForSourcesAsync(indexed, embedder.Model, cancellationToken);
            var scored = candidates
                .Select(c => (Chunk: c, Score: Chunker.Cosine(query, c.ReadVector())))
                .OrderByDescending(x => x.Score)
                .ToList();

            // Keep passages that score at least half as well as the best one: close matches only.
            var top = scored.Count == 0 ? 0 : scored[0].Score;
            var best = scored.Where(x => x.Score > 0 && x.Score >= top * RelativeCutoff).Take(TopPassages).ToList();

            var total = 0;
            var picked = new List<ContextChunk>();
            foreach (var (chunk, _) in best)
            {
                if (total + chunk.Text.Length > MaxCharacters)
                {
                    break;
                }

                total += chunk.Text.Length;
                picked.Add(chunk);
            }

            // Every indexed source appears, even with no passages: retrieval found nothing relevant there.
            return indexed.ToDictionary(
                id => id,
                id => (IReadOnlyList<string>)[.. picked.Where(c => c.SourceId == id).OrderBy(c => c.Index).Select(c => c.Text)]);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            // When embedding fails, fall back to the character budget rather than failing the chat.
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }
    }

    public static string Join(IReadOnlyList<string> passages) =>
        passages.Aggregate(new StringBuilder(), (sb, p) => sb.Append(sb.Length > 0 ? "\n[...]\n" : string.Empty).Append(p)).ToString();
}
