using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Orbs.Application.Providers;
using Orbs.Application.Voice;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class VoiceSteps(SpecContext context)
{
    private string? _heard;

    [Given("a provider that can listen and speak")]
    public Task GivenSpeech() =>
        context.Api.CreateProviderAsync(new SaveProviderRequest("Echo", "echo", DefaultModel: "echo-1", SpeechToTextModel: "ears", TextToSpeechModel: "voice"));

    [When("I say out loud {string}")]
    public async Task WhenISayOutLoud(string words)
    {
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(Encoding.UTF8.GetBytes(words));
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        form.Add(audio, "audio", "speech.webm");
        _heard = (await ApiClient.ReadAsync<TranscriptionDto>(await context.Api.Http.PostAsync(new Uri("/api/voice/transcribe", UriKind.Relative), form))).Text;
    }

    [Then("Orbs hears {string}")]
    public void ThenOrbsHears(string words) => _heard.ShouldBe(words);

    [Then("Orbs can read the answer aloud")]
    public async Task ThenOrbsCanSpeak()
    {
        var response = await context.Api.Http.PostAsJsonAsync("/api/voice/speak", new SpeakRequest("Good morning to you"), ApiClient.Json);
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldStartWith("audio/");
    }
}
