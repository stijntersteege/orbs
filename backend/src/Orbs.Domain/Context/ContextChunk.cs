using Orbs.Domain.Common;

namespace Orbs.Domain.Context;

/// <summary>
/// One passage of a context source with its embedding, for retrieval: when someone asks a question,
/// the passages closest to it go into the prompt instead of the whole source.
/// </summary>
public sealed class ContextChunk : OwnedAggregateRoot
{
    public const int MaxTextLength = 4_000;

    private ContextChunk(Guid id, Guid sourceId, int index, string text, byte[] vector, string model, DateTimeOffset now) : base(id, now)
    {
        SourceId = sourceId;
        Index = index;
        Text = text;
        Vector = vector;
        Model = model;
    }

    // For EF Core materialization.
    private ContextChunk()
    {
        Text = string.Empty;
        Vector = [];
        Model = string.Empty;
    }

    public Guid SourceId { get; private set; }

    /// <summary>The passage's position in the source.</summary>
    public int Index { get; private set; }

    public string Text { get; private set; }

#pragma warning disable CA1819 // The embedding, stored as little-endian float32 bytes.
    public byte[] Vector { get; private set; }
#pragma warning restore CA1819

    /// <summary>The embedding model that made <see cref="Vector"/>. Vectors of different models never mix.</summary>
    public string Model { get; private set; }

    public static ContextChunk Create(Guid sourceId, int index, string text, float[] vector, string model, DateTimeOffset now)
    {
        Guard.NotEmpty(sourceId, nameof(sourceId));
        ArgumentNullException.ThrowIfNull(vector);
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return new ContextChunk(Guid.NewGuid(), sourceId, index, Guard.Text(text, nameof(Text), MaxTextLength), bytes, Guard.Text(model, nameof(Model), 200), now);
    }

    public float[] ReadVector()
    {
        var vector = new float[Vector.Length / sizeof(float)];
        Buffer.BlockCopy(Vector, 0, vector, 0, Vector.Length);
        return vector;
    }
}
