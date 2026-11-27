using ModelContextProtocol.Protocol;
using Orbs.Infrastructure.Tools;

namespace Orbs.Infrastructure.Tests;

public class McpClientTests
{
    [Fact]
    public void Command_lines_keep_quoted_parts_together()
    {
        McpClientAdapter.Split("""npx -y "@scope/server name" --dir '/my files'""")
            .ShouldBe(["npx", "-y", "@scope/server name", "--dir", "/my files"]);
    }

    [Fact]
    public void Arguments_must_be_an_object()
    {
        McpClientAdapter.ParseArguments("""{"a":1}""").Keys.ShouldBe(["a"]);
        McpClientAdapter.ParseArguments("").ShouldBeEmpty();
        Should.Throw<ArgumentException>(() => McpClientAdapter.ParseArguments("[1]"));
    }

    [Fact]
    public void Results_are_flattened_to_text()
    {
        var result = new CallToolResult { Content = [new TextContentBlock { Text = "one" }, new TextContentBlock { Text = "two" }] };

        McpClientAdapter.Flatten(result).ShouldBe("one" + Environment.NewLine + "two");
    }
}
