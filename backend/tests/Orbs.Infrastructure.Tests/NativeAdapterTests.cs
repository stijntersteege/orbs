using System.Text.Json.Nodes;
using Orbs.Application.Abstractions;
using Orbs.Infrastructure.Models;

namespace Orbs.Infrastructure.Tests;

public class NativeAdapterTests
{
    private static readonly ChatRequest ToolConversation = new(
        "model-x",
        [
            new ChatTurn(ChatRole.System, "You are Pip."),
            new ChatTurn(ChatRole.User, "What is 6*7 and the time?"),
            new ChatTurn(ChatRole.Assistant, "Let me check.", [new ToolCall("c1", "calculator", """{"expression":"6*7"}"""), new ToolCall("c2", "current_time", "{}")]),
            new ChatTurn(ChatRole.Tool, "42", ToolCallId: "c1"),
            new ChatTurn(ChatRole.Tool, "noon", ToolCallId: "c2"),
        ],
        [new ToolDefinition("calculator", "Sums", """{"$schema":"x","type":"object","additionalProperties":false,"properties":{"title":{"type":"string","title":"T"}}}""")]);

    [Fact]
    public void Anthropic_keeps_the_system_prompt_apart_and_groups_tool_results()
    {
        var body = AnthropicModelAdapter.BuildRequest(ToolConversation);

        body["system"]!.GetValue<string>().ShouldBe("You are Pip.");
        var messages = body["messages"]!.AsArray();
        messages.Select(m => m!["role"]!.GetValue<string>()).ShouldBe(["user", "assistant", "user"]);
        var assistant = messages[1]!["content"]!.AsArray();
        assistant[1]!["type"]!.GetValue<string>().ShouldBe("tool_use");
        assistant[1]!["input"]!["expression"]!.GetValue<string>().ShouldBe("6*7");
        var results = messages[2]!["content"]!.AsArray();
        results.Count.ShouldBe(2);
        results[0]!["tool_use_id"]!.GetValue<string>().ShouldBe("c1");
        body["tools"]![0]!["input_schema"]!["type"]!.GetValue<string>().ShouldBe("object");
    }

    [Fact]
    public void Anthropic_streams_text_and_assembles_tool_input()
    {
        var parser = new AnthropicModelAdapter.StreamParser();
        string[] events =
        [
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hi"}}""",
            """{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_1","name":"calculator","input":{}}}""",
            """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"expression\":"}}""",
            """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"\"1+1\"}"}}""",
            """{"type":"content_block_stop","index":1}""",
            """{"type":"message_stop"}""",
        ];
        var outputs = events.SelectMany(parser.Read).ToList();

        outputs.ShouldBe([new TextOutput("Hi"), new ToolCallOutput(new ToolCall("toolu_1", "calculator", """{"expression":"1+1"}"""))]);
    }

    [Fact]
    public void Anthropic_stream_errors_surface()
    {
        Should.Throw<HttpRequestException>(() => new AnthropicModelAdapter.StreamParser().Read("""{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""").ToList())
            .Message.ShouldBe("Overloaded");
    }

    [Fact]
    public void Gemini_names_function_responses_after_their_calls_and_cleans_schemas()
    {
        var body = GeminiModelAdapter.BuildRequest(ToolConversation);

        body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>().ShouldBe("You are Pip.");
        var contents = body["contents"]!.AsArray();
        contents.Select(c => c!["role"]!.GetValue<string>()).ShouldBe(["user", "model", "user"]);
        contents[1]!["parts"]![1]!["functionCall"]!["name"]!.GetValue<string>().ShouldBe("calculator");
        var responses = contents[2]!["parts"]!.AsArray();
        responses.Select(p => p!["functionResponse"]!["name"]!.GetValue<string>()).ShouldBe(["calculator", "current_time"]);

        var parameters = body["tools"]![0]!["functionDeclarations"]![0]!["parameters"]!.AsObject();
        parameters.ContainsKey("$schema").ShouldBeFalse();
        parameters.ContainsKey("additionalProperties").ShouldBeFalse();
        var titleProperty = parameters["properties"]!["title"]!.AsObject();
        titleProperty.ContainsKey("type").ShouldBeTrue("a property called title stays");
        titleProperty.ContainsKey("title").ShouldBeFalse("the title keyword goes");
    }

    [Fact]
    public void Gemini_chunks_yield_text_and_function_calls()
    {
        var calls = 0;
        var outputs = GeminiModelAdapter.ParseChunk(
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"Hi "},{"text":"secret plan","thought":true},{"functionCall":{"name":"calculator","args":{"expression":"2+2"}}}]}}]}""",
            ref calls);

        outputs.ShouldBe([new TextOutput("Hi "), new ToolCallOutput(new ToolCall("call_1", "calculator", """{"expression":"2+2"}"""))]);
    }

    [Fact]
    public void Gemini_schema_cleaning_reaches_nested_items()
    {
        var schema = JsonNode.Parse("""{"type":"array","items":{"type":"object","additionalProperties":true,"properties":{"a":{"type":"string","default":"x"}}}}""");

        var clean = GeminiModelAdapter.CleanSchema(schema)!.ToJsonString();

        clean.ShouldBe("""{"type":"array","items":{"type":"object","properties":{"a":{"type":"string"}}}}""");
    }
}
