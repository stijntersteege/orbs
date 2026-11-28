using Orbs.Application.Abstractions;
using Orbs.Domain.Context;

namespace Orbs.Application.Context;

public sealed record ContextKindDto(string Kind, string DisplayName, string Description, bool UsesLocation, bool UsesInput);

public sealed record ContextSourceDto(
    Guid Id,
    string Name,
    string Kind,
    string Location,
    string Input,
    ContextSourceStatus Status,
    string? LastError,
    DateTimeOffset? LastSyncedAt,
    int ContentLength,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int IndexedChunks = 0,
    SyncInterval SyncEvery = SyncInterval.Off,
    DateTimeOffset? NextSyncAt = null,
    DateTimeOffset? IndexedAt = null);

/// <summary>How a source can be changed in the app.</summary>
public enum ContextEditing
{
    /// <summary>Only its name: web pages are read again instead, and PDFs, Word documents and pictures are replaced by uploading.</summary>
    NameOnly,

    /// <summary>A note: its text is the source.</summary>
    Note,

    /// <summary>A text file (Markdown, plain text, CSV, JSON): its text is saved back into the file.</summary>
    TextFile,
}

/// <summary>
/// What a source holds, as orbs read it: the note, the page's text or the file's text. Files also say what they are.
/// <see cref="Truncated"/> means the text was cut at the limit orbs read.
/// </summary>
public sealed record ContextContentDto(
    Guid Id,
    string Kind,
    string Content,
    bool Truncated,
    ContextEditing Editing,
    string? FileName = null,
    string? ContentType = null,
    long? Size = null);

/// <summary>New text for a text file.</summary>
public sealed record ReplaceFileTextRequest(string Text);

/// <summary>Adds or changes a source. <see cref="SyncEvery"/> only applies to kinds that read from a location, such as web pages.</summary>
public sealed record SaveContextSourceRequest(string Name, string Kind, string? Location = null, string? Input = null, SyncInterval? SyncEvery = null);

internal static class ContextMapping
{
    public static ContextSourceDto ToDto(this ContextSource s) => new(
        s.Id, s.Name, s.Kind, s.Location, s.Input, s.Status, s.LastError, s.LastSyncedAt, s.Content.Length, s.CreatedAt, s.UpdatedAt, s.IndexedChunks, s.SyncEvery, s.NextSyncAt, s.IndexedAt);

    public static ContextKindDto ToDto(this IContextConnector c) => new(
        c.Kind, c.DisplayName, c.Description, c.UsesLocation, c.UsesInput);
}
