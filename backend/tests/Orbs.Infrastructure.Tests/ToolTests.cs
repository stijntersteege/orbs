using Orbs.Application.Abstractions;
using Orbs.Infrastructure.Models;
using Orbs.Infrastructure.Tools;

namespace Orbs.Infrastructure.Tests;

public class ToolTests
{
    private static ToolInvocation Args(string json) => new(Guid.NewGuid(), null, json);

    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("2 ^ 3 ^ 2", "512")]
    [InlineData("-4 + 10 / 4", "-1.5")]
    [InlineData("10 % 4", "2")]
    [InlineData(" 12.5*4 ", "50")]
    public async Task The_calculator_follows_the_usual_rules(string expression, string expected)
    {
        (await new CalculatorTool().InvokeAsync(Args($$"""{"expression":"{{expression}}"}"""))).ShouldBe(expected);
    }

    [Theory]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("2 * x")]
    [InlineData("1 / 0")]
    public async Task The_calculator_refuses_what_it_cannot_evaluate(string expression)
    {
        await Should.ThrowAsync<ArgumentException>(() => new CalculatorTool().InvokeAsync(Args($$"""{"expression":"{{expression}}"}""")));
    }

    [Fact]
    public async Task The_time_tool_knows_time_zones()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var tool = new CurrentTimeTool(clock);

        (await tool.InvokeAsync(Args("{}"))).ShouldBe("Wednesday 2026-07-01 12:00 UTC");
        (await tool.InvokeAsync(Args("""{"timeZone":"Europe/Amsterdam"}"""))).ShouldStartWith("Wednesday 2026-07-01 14:00");
        await Should.ThrowAsync<ArgumentException>(() => tool.InvokeAsync(Args("""{"timeZone":"Mars/Olympus"}""")));
    }

    [Fact]
    public void Streamed_tool_call_pieces_are_joined_by_index()
    {
        var calls = new ToolCallAccumulator();
        calls.Add("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_a","function":{"name":"calculator","arguments":""}}]}}]}""");
        calls.Add("""{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"expression\":"}}]}}]}""");
        calls.Add("""{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"1+1\"}"}}]}}]}""");
        calls.Add("""{"choices":[{"delta":{"tool_calls":[{"index":1,"id":"call_b","function":{"name":"current_time"}}]}}]}""");

        var done = calls.Complete();

        done.Count.ShouldBe(2);
        done[0].ShouldBe(new ToolCall("call_a", "calculator", """{"expression":"1+1"}"""));
        done[1].ShouldBe(new ToolCall("call_b", "current_time", "{}"));
    }

    [Fact]
    public void Requests_carry_tools_and_earlier_tool_turns()
    {
        var request = new ChatRequest(
            "gpt",
            [
                new ChatTurn(ChatRole.User, "what time is it"),
                new ChatTurn(ChatRole.Assistant, "", [new ToolCall("c1", "current_time", "{}")]),
                new ChatTurn(ChatRole.Tool, "noon", ToolCallId: "c1"),
            ],
            [new ToolDefinition("current_time", "Get the time", """{"type":"object"}""")]);

        var json = OpenAiCompatibleModelAdapter.BuildRequest(request).ToJsonString();

        json.ShouldContain("\"tools\":[{\"type\":\"function\",\"function\":{\"name\":\"current_time\"");
        json.ShouldContain("\"tool_calls\":[{\"id\":\"c1\"");
        json.ShouldContain("\"role\":\"tool\",\"content\":\"noon\",\"tool_call_id\":\"c1\"");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
