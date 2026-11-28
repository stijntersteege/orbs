using Orbs.Application.Voice;

namespace Orbs.Api.Endpoints;

internal static class VoiceEndpoints
{
    public static void MapVoiceEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/voice").WithTags("Voice");

        group.MapGet("/", (VoiceService voice, CancellationToken ct) => voice.StatusAsync(ct))
            .WithSummary("Whether Orbs can listen and speak, and which voices to suggest");

        group.MapPost("/transcribe", async (IFormFile audio, [Microsoft.AspNetCore.Mvc.FromForm] string? language, VoiceService voice, CancellationToken ct) =>
            {
                await using var stream = audio.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                return await voice.TranscribeAsync(buffer.ToArray(), audio.ContentType, language, ct);
            })
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(VoiceService.MaxAudioBytes + 1_000_000))
            .WithSummary("Turn a recording (multipart field 'audio') into text");

        group.MapPost("/speak", async (SpeakRequest request, VoiceService voice, HttpContext http, CancellationToken ct) =>
            {
                var audio = await voice.SpeakAsync(request, ct);
                http.Response.Headers.XContentTypeOptions = "nosniff";
                return TypedResults.File(audio.Data, audio.ContentType);
            })
            .WithSummary("Read text aloud; answers audio");
    }
}
