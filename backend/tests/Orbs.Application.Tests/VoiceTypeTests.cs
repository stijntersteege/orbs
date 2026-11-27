using Orbs.Application.Voice;

namespace Orbs.Application.Tests;

public class VoiceTypeTests
{
    [Theory]
    [InlineData("audio/webm;codecs=opus", "audio/webm")]
    [InlineData("AUDIO/MP4", "audio/mp4")]
    [InlineData("audio/x-m4a", "audio/mp4")]
    [InlineData(null, "audio/webm")]
    [InlineData("text/html", "audio/webm")]
    [InlineData("audio/x\r\nInjected: yes", "audio/webm")]
    public void Recordings_only_keep_known_audio_types(string? sent, string expected) =>
        VoiceService.RecordingType(sent).ShouldBe(expected);

    [Theory]
    [InlineData("audio/mpeg", "audio/mpeg")]
    [InlineData("audio/wav; charset=binary", "audio/wav")]
    [InlineData("text/html", "audio/mpeg")]
    [InlineData("application/javascript", "audio/mpeg")]
    [InlineData("", "audio/mpeg")]
    public void Spoken_audio_is_never_served_as_something_else(string answered, string expected) =>
        VoiceService.SpokenType(answered).ShouldBe(expected);
}
