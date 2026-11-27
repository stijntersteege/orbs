using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Orbs.Application.Abstractions;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Application.Voice;
using Orbs.Infrastructure.Models;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class OrbVoiceTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public OrbVoiceTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private EchoModelAdapter Echo => _factory.Services.GetServices<IModelAdapter>().OfType<EchoModelAdapter>().Single();

    [Fact]
    public async Task An_orb_speaks_in_its_own_voice()
    {
        await _api.CreateProviderAsync(new SaveProviderRequest("Echo", "echo", DefaultModel: "echo-1", TextToSpeechModel: "voice"));
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip", Voice: "nova"));
        orb.Voice.ShouldBe("nova");

        (await _api.Http.PostAsJsonAsync("/api/voice/speak", new SpeakRequest("Hi", OrbId: orb.Id), ApiClient.Json)).EnsureSuccessStatusCode();
        Echo.LastVoice.ShouldBe("nova");

        (await _api.Http.PostAsJsonAsync("/api/voice/speak", new SpeakRequest("Hi", Voice: "onyx", OrbId: orb.Id), ApiClient.Json)).EnsureSuccessStatusCode();
        Echo.LastVoice.ShouldBe("onyx", "an explicit voice, as in the preview, wins");
    }

    [Fact]
    public async Task Without_a_voice_the_provider_default_is_used()
    {
        await _api.CreateProviderAsync(new SaveProviderRequest("Echo", "echo", DefaultModel: "echo-1", TextToSpeechModel: "voice"));
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));

        (await _api.Http.PostAsJsonAsync("/api/voice/speak", new SpeakRequest("Hi", OrbId: orb.Id), ApiClient.Json)).EnsureSuccessStatusCode();

        Echo.LastVoice.ShouldBe("echo");
    }
}
