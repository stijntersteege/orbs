using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Orbs.Application.Providers;
using Orbs.Application.Voice;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class VoiceApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public VoiceApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private Task<ProviderDto> EchoWithSpeechAsync() =>
        _api.CreateProviderAsync(new SaveProviderRequest("Echo", "echo", DefaultModel: "echo-1", SpeechToTextModel: "echo-ears", TextToSpeechModel: "echo-voice"));

    [Fact]
    public async Task Without_speech_models_voice_is_off()
    {
        await _api.ConnectEchoAsync();

        var status = await _api.GetAsync<VoiceStatusDto>("/api/voice");

        status.CanListen.ShouldBeFalse();
        status.CanSpeak.ShouldBeFalse();
        (await _api.Http.PostAsJsonAsync("/api/voice/speak", new SpeakRequest("hi"), ApiClient.Json)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_recording_becomes_text()
    {
        await EchoWithSpeechAsync();
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(Encoding.UTF8.GetBytes("What is on my calendar?"));
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        form.Add(audio, "audio", "speech.webm");

        var result = await ApiClient.ReadAsync<TranscriptionDto>(await _api.Http.PostAsync(new Uri("/api/voice/transcribe", UriKind.Relative), form));

        result.Text.ShouldBe("What is on my calendar?");
    }

    [Fact]
    public async Task Text_is_read_aloud_as_audio()
    {
        await EchoWithSpeechAsync();

        var response = await _api.Http.PostAsJsonAsync("/api/voice/speak", new SpeakRequest("Hello there", "echo"), ApiClient.Json);

        response.Content.Headers.ContentType!.MediaType.ShouldBe("audio/wav");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("RIFF");
        (await _api.GetAsync<VoiceStatusDto>("/api/voice")).Voices.ShouldBe(["echo"]);
    }
}
