using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Context;

namespace Orbs.Application.Context;

public interface IContextFileRepository : IRepository<ContextFile>
{
    Task<ContextFile?> FindBySourceAsync(Guid sourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContextFile>> ListImagesAsync(IReadOnlyCollection<Guid> sourceIds, CancellationToken cancellationToken = default);
}

/// <summary>Turns a document into plain text. Knows PDF, Word (.docx), Markdown and other plain text.</summary>
public interface IDocumentReader
{
    /// <summary>The content type Orbs uses for this file, worked out from its name and the browser's guess.</summary>
    string ContentTypeFor(string fileName, string? declared);

    bool CanRead(string contentType);

    Task<string> ReadTextAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default);
}

/// <summary>An uploaded file, as the upload endpoint passes it on.</summary>
public sealed record UploadedFile(string FileName, string? ContentType, byte[] Data);

/// <summary>Use cases for files as context: upload, read into text, and the images orbs can look at.</summary>
public sealed class FileContextService(
    IContextSourceRepository sources,
    IContextFileRepository files,
    IDocumentReader reader,
    ContextIndexer indexer,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string Kind = "file";

    /// <summary>At most this many images go to a model with one message.</summary>
    public const int MaxImagesPerMessage = 4;

    public async Task<ContextSourceDto> UploadAsync(string? name, UploadedFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var contentType = reader.ContentTypeFor(file.FileName, file.ContentType);
        var isImage = contentType.StartsWith("image/", StringComparison.Ordinal);
        if (!isImage && !reader.CanRead(contentType))
        {
            throw new DomainException($"Orbs cannot read {Path.GetExtension(file.FileName)} files yet. Try PDF, Word (.docx), Markdown, plain text or an image.");
        }

        var now = clock.GetUtcNow();
        var source = ContextSource.Create(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name, Kind, now);
        source.Configure(file.FileName, null, now);
        var stored = ContextFile.Create(source.Id, file.FileName, contentType, file.Data, now);

        try
        {
            var text = isImage
                ? $"[Image: {stored.FileName}. Orbs whose model can see get the picture itself.]"
                : await reader.ReadTextAsync(stored.FileName, contentType, file.Data, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("No text found. Scanned PDFs need text recognition first.");
            }

            source.MarkSynced(text, now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            source.MarkFailed(ex.Message, now);
        }

        sources.Add(source);
        files.Add(stored);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (!isImage)
        {
            await indexer.QueueAsync(source, cancellationToken);
        }

        return source.ToDto();
    }

    /// <summary>What a source holds and how it can be edited. Works for every kind; files also bring their name, type and size.</summary>
    public async Task<ContextContentDto> ReadAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        var source = await sources.FindAsync(sourceId, cancellationToken) ?? throw new NotFoundException("Context source", sourceId);
        var truncated = source.Content.Length >= ContextSource.MaxContentLength;
        if (source.Kind != Kind)
        {
            var isNote = source.Kind == "text";
            return new ContextContentDto(source.Id, source.Kind, isNote ? source.Input : source.Content, truncated, isNote ? ContextEditing.Note : ContextEditing.NameOnly);
        }

        var file = await files.FindBySourceAsync(sourceId, cancellationToken);
        if (file is null)
        {
            return new ContextContentDto(source.Id, source.Kind, source.Content, truncated, ContextEditing.NameOnly);
        }

        // An editable text file shows exactly what is in it, so saving it back changes nothing by accident.
        var content = file.IsEditableText ? DecodeText(file.Data) : source.Content;
        return new ContextContentDto(
            source.Id,
            source.Kind,
            content,
            truncated && !file.IsEditableText,
            file.IsEditableText ? ContextEditing.TextFile : ContextEditing.NameOnly,
            file.FileName,
            file.ContentType,
            file.Size);
    }

    /// <summary>Saves edited text back into a text file, reads it again and refreshes what orbs search.</summary>
    public async Task<ContextContentDto> ReplaceTextAsync(Guid sourceId, ReplaceFileTextRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = await sources.FindAsync(sourceId, cancellationToken) ?? throw new NotFoundException("Context source", sourceId);
        var file = await files.FindBySourceAsync(sourceId, cancellationToken) ?? throw new NotFoundException("This source has no file.");
        var now = clock.GetUtcNow();
        file.ReplaceText(System.Text.Encoding.UTF8.GetBytes(request.Text ?? string.Empty), now);
        source.MarkSynced(await reader.ReadTextAsync(file.FileName, file.ContentType, file.Data, cancellationToken), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await indexer.QueueAsync(source, cancellationToken);
        return await ReadAsync(sourceId, cancellationToken);
    }

    private static string DecodeText(byte[] data) =>
        new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(data).TrimStart('\uFEFF');

    public async Task<ContextFile> DownloadAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        _ = await sources.FindAsync(sourceId, cancellationToken) ?? throw new NotFoundException("Context source", sourceId);
        return await files.FindBySourceAsync(sourceId, cancellationToken) ?? throw new NotFoundException("This source has no file.");
    }

    /// <summary>The pictures among an orb's sources, for models that can see.</summary>
    public async Task<IReadOnlyList<ChatImage>> ImagesForAsync(IReadOnlyList<ContextSource> context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ids = context.Where(s => s.Kind == Kind && s.Status == ContextSourceStatus.Ready).Select(s => s.Id).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return [.. (await files.ListImagesAsync(ids, cancellationToken)).Take(MaxImagesPerMessage).Select(f => new ChatImage(f.ContentType, f.Data, f.FileName))];
    }
}
