using Orbs.Application.Context;

namespace Orbs.Api.Endpoints;

internal static class ContextEndpoints
{
    public static void MapContextEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/context-sources").WithTags("Context");

        group.MapGet("/kinds", (ContextService context) => context.ListKinds())
            .WithSummary("List the kinds of context this server can read");

        group.MapGet("/", (ContextService context, CancellationToken ct) => context.ListAsync(ct))
            .WithSummary("List context sources");

        group.MapGet("/{id:guid}", (Guid id, ContextService context, CancellationToken ct) => context.GetAsync(id, ct))
            .WithSummary("Get one context source");

        group.MapPost("/", async (SaveContextSourceRequest request, ContextService context, CancellationToken ct) =>
            {
                var source = await context.CreateAsync(request, ct);
                return TypedResults.Created($"/api/context-sources/{source.Id}", source);
            })
            .WithSummary("Add a context source and read it");

        group.MapPut("/{id:guid}", (Guid id, SaveContextSourceRequest request, ContextService context, CancellationToken ct) => context.UpdateAsync(id, request, ct))
            .WithSummary("Update a context source and read it again");

        group.MapGet("/{id:guid}/content", (Guid id, FileContextService files, CancellationToken ct) => files.ReadAsync(id, ct))
            .WithSummary("What a source holds, as orbs read it, and how it can be edited");

        group.MapPut("/{id:guid}/file", (Guid id, ReplaceFileTextRequest request, FileContextService files, CancellationToken ct) => files.ReplaceTextAsync(id, request, ct))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(Orbs.Domain.Context.ContextFile.MaxBytes + 1_000_000))
            .WithSummary("Save edited text back into a text file (Markdown, plain text, CSV, JSON)");

        group.MapPost("/{id:guid}/sync", (Guid id, ContextService context, CancellationToken ct) => context.SyncAsync(id, ct))
            .WithSummary("Read a context source again");

        group.MapPost("/files", async (IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] string? name, FileContextService files, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                var source = await files.UploadAsync(name, new UploadedFile(file.FileName, file.ContentType, buffer.ToArray()), ct);
                return TypedResults.Created($"/api/context-sources/{source.Id}", source);
            })
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(Orbs.Domain.Context.ContextFile.MaxBytes + 1_000_000))
            .WithSummary("Upload a file as context: PDF, Word (.docx), Markdown, plain text or an image");

        group.MapGet("/{id:guid}/file", async (Guid id, FileContextService files, CancellationToken ct) =>
            {
                var file = await files.DownloadAsync(id, ct);
                return TypedResults.File(file.Data, SafeDownloadType(file.ContentType), file.FileName);
            })
            .WithSummary("Download the file behind a file source");

        group.MapPost("/{id:guid}/index", async (Guid id, ContextService context, CancellationToken ct) =>
                await context.IndexAsync(id, ct)
                    ? Results.Accepted()
                    : Results.Problem("Set an embedding model on one of your providers first, and make sure the source is synced.", statusCode: StatusCodes.Status409Conflict))
            .WithSummary("Embed a context source again for retrieval (runs in the background)");

        group.MapDelete("/{id:guid}", async (Guid id, ContextService context, CancellationToken ct) =>
            {
                await context.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Remove a context source from every orb and delete it");
    }

    private static readonly HashSet<string> PassiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/json",
        "text/plain",
        "text/markdown",
        "text/csv",
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
    };

    /// <summary>
    /// Uploaded files keep their type only when it cannot run in a browser. HTML, SVG and anything unknown
    /// download as plain bytes, so an upload can never act as a page on the Orbs address.
    /// </summary>
    internal static string SafeDownloadType(string contentType) =>
        PassiveTypes.Contains(contentType) ? contentType : "application/octet-stream";
}
