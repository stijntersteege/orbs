using Orbs.Application.Abstractions;
using Orbs.Infrastructure.Context;
using Orbs.Infrastructure.Models;

namespace Orbs.Infrastructure.Tests;

/// <summary>Unit tests for the pure parts of the infrastructure adapters.</summary>
public class AdapterTests
{
    [Fact]
    public void OpenAi_stream_chunks_yield_their_text()
    {
        OpenAiCompatibleModelAdapter.ParseDelta("""{"choices":[{"delta":{"content":"Hi"}}]}""").ShouldBe("Hi");
    }

    [Theory]
    [InlineData("""{"choices":[]}""")]
    [InlineData("""{"choices":[{"delta":{}}]}""")]
    [InlineData("""{"choices":[{"delta":{"content":null}}]}""")]
    [InlineData("""{"id":"x"}""")]
    public void OpenAi_chunks_without_text_yield_nothing(string json)
    {
        OpenAiCompatibleModelAdapter.ParseDelta(json).ShouldBeNull();
    }

    [Theory]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/chat/completions")]
    [InlineData("https://openrouter.ai/api/v1/", "https://openrouter.ai/api/v1/chat/completions")]
    public void OpenAi_endpoints_keep_the_base_path(string baseUrl, string expected)
    {
        OpenAiCompatibleModelAdapter.Endpoint(new ProviderConnection(new Uri(baseUrl), null), "chat/completions")
            .ToString().ShouldBe(expected);
    }

    [Fact]
    public void Html_becomes_readable_text()
    {
        const string html = "<html><head><title>x</title></head><body><script>evil()</script><h1>Roadmap</h1><p>Ship &amp; celebrate</p></body></html>";

        var text = UrlConnector.HtmlToText(html);

        text.ShouldContain("Roadmap");
        text.ShouldContain("Ship & celebrate");
        text.ShouldNotContain("evil");
        text.ShouldNotContain("<");
    }

    [Fact]
    public async Task Echo_repeats_the_last_user_message()
    {
        var echo = new EchoModelAdapter { WordDelay = TimeSpan.Zero };
        var request = new ChatRequest("echo-1", [new ChatTurn(ChatRole.System, "sys"), new ChatTurn(ChatRole.User, "hello world")]);

        var parts = new List<string>();
        await foreach (var part in echo.StreamChatAsync(new ProviderConnection(null, null), request))
        {
            parts.Add(part);
        }

        string.Concat(parts).ShouldBe("You said: hello world");
    }
}
