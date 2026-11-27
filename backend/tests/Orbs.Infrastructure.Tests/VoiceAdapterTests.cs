using Orbs.Infrastructure.Models;

namespace Orbs.Infrastructure.Tests;

public class VoiceAdapterTests
{
    [Theory]
    [InlineData("audio/webm;codecs=opus", ".webm")]
    [InlineData("audio/mp4", ".m4a")]
    [InlineData("audio/ogg", ".ogg")]
    [InlineData("application/octet-stream", ".webm")]
    public void Recordings_get_a_file_name_the_whisper_api_recognizes(string contentType, string extension)
    {
        OpenAiCompatibleModelAdapter.AudioExtension(contentType).ShouldBe(extension);
    }

    [Fact]
    public void The_demo_voice_is_a_valid_wav_file()
    {
        var wav = EchoModelAdapter.SilentWav(TimeSpan.FromSeconds(1));

        wav.Length.ShouldBe(44 + 8_000);
        BitConverter.ToInt32(wav, 40).ShouldBe(8_000);
    }
}
