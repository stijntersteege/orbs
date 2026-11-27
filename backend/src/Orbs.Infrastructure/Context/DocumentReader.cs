using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Orbs.Application.Abstractions;
using Orbs.Application.Context;
using Orbs.Domain.Context;
using UglyToad.PdfPig;

namespace Orbs.Infrastructure.Context;

/// <summary>Reads PDF (PdfPig), Word .docx (its XML, no Office needed) and plain-text formats.</summary>
internal sealed class DocumentReader : IDocumentReader
{
    private const string Pdf = "application/pdf";
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = Pdf,
        [".docx"] = Docx,
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    public string ContentTypeFor(string fileName, string? declared) =>
        ByExtension.TryGetValue(Path.GetExtension(fileName ?? string.Empty), out var type)
            ? type
            : string.IsNullOrWhiteSpace(declared) ? "application/octet-stream" : declared.Split(';')[0].Trim().ToLowerInvariant();

    public bool CanRead(string contentType) =>
        contentType is Pdf or Docx or "application/json" || contentType.StartsWith("text/", StringComparison.Ordinal);

    public Task<string> ReadTextAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var text = contentType switch
        {
            Pdf => ReadPdf(data),
            Docx => ReadDocx(data),
            "text/html" => UrlConnector.HtmlToText(Decode(data)),
            _ => Decode(data),
        };
        return Task.FromResult(text.Length > ContextSource.MaxContentLength ? text[..ContextSource.MaxContentLength] : text.Trim());
    }

    internal static string ReadPdf(byte[] data)
    {
        using var document = PdfDocument.Open(data);
        var text = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            text.AppendLine(page.Text).AppendLine();
        }

        return text.ToString();
    }

    /// <summary>Paragraph text from word/document.xml; good enough for reading, without layout.</summary>
    internal static string ReadDocx(byte[] data)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("This is not a Word document.");
        using var stream = entry.Open();
        var xml = XDocument.Load(stream);
        var text = new StringBuilder();
        foreach (var paragraph in xml.Descendants(w + "p"))
        {
            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == w + "t")
                {
                    text.Append(node.Value);
                }
                else if (node.Name == w + "tab")
                {
                    text.Append('\t');
                }
                else if (node.Name == w + "br")
                {
                    text.AppendLine();
                }
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    private static string Decode(byte[] data) => new UTF8Encoding(false).GetString(data).TrimStart('﻿');
}

/// <summary>Uploaded files. Read once on upload, so syncing keeps what is there.</summary>
internal sealed class FileConnector : IContextConnector
{
    public string Kind => FileContextService.Kind;

    public string DisplayName => "File";

    public string Description => "Upload a PDF, a Word or Markdown document, or an image.";

    public bool UsesLocation => false;

    public bool UsesInput => false;

    public Task<string> ReadAsync(ContextSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Task.FromResult(source.Content);
    }
}
