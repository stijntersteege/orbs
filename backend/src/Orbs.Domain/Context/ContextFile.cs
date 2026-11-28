using Orbs.Domain.Common;

namespace Orbs.Domain.Context;

/// <summary>
/// The uploaded file behind a "file" context source: a PDF, a Word or Markdown document, or an image.
/// Documents are read into text on upload; images are shown to models that can see.
/// </summary>
public sealed class ContextFile : OwnedAggregateRoot
{
    public const long MaxBytes = 20 * 1024 * 1024;
    public const int MaxFileNameLength = 255;

    private ContextFile(Guid id, Guid sourceId, string fileName, string contentType, byte[] data, DateTimeOffset now) : base(id, now)
    {
        SourceId = sourceId;
        FileName = fileName;
        ContentType = contentType;
        Data = data;
    }

    // For EF Core materialization.
    private ContextFile()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
        Data = [];
    }

    public Guid SourceId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

#pragma warning disable CA1819 // The file's bytes, stored as-is.
    public byte[] Data { get; private set; }
#pragma warning restore CA1819

    public long Size => Data.LongLength;

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public static ContextFile Create(Guid sourceId, string fileName, string contentType, byte[] data, DateTimeOffset now)
    {
        Guard.NotEmpty(sourceId, nameof(sourceId));
        ArgumentNullException.ThrowIfNull(data);
        if (data.LongLength == 0)
        {
            throw new DomainException("The file is empty.");
        }

        if (data.LongLength > MaxBytes)
        {
            throw new DomainException($"Files can be at most {MaxBytes / 1024 / 1024} MB.");
        }

        var name = Path.GetFileName(Guard.Text(fileName, nameof(FileName), MaxFileNameLength));
        return new ContextFile(Guid.NewGuid(), sourceId, name, Guard.Text(contentType, nameof(ContentType), 100), data, now);
    }
}
