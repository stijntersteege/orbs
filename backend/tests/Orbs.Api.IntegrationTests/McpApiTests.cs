using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Application.Tools;
using Orbs.Domain.Orbs;
using Orbs.Domain.Tools;
using Orbs.Infrastructure.Tools;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class McpApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public McpApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private async Task<McpServerDto> ConnectAsync(string name, string url, string? auth = null) =>
        await ApiClient.ReadAsync<McpServerDto>(await _api.Http.PostAsJsonAsync("/api/mcp-servers", new SaveMcpServerRequest(name, McpTransport.Http, url, auth), ApiClient.Json));

    [Fact]
    public async Task A_connected_server_adds_its_tools_with_the_server_in_the_name()
    {
        var server = await ConnectAsync("Home Lab", "https://mcp.example.com/mcp", "Bearer secret-token");

        server.HasAuthorization.ShouldBeTrue();
        (await _api.Http.GetStringAsync(new Uri("/api/mcp-servers", UriKind.Relative))).ShouldNotContain("secret-token");
        var tools = await _api.GetAsync<List<ToolDto>>("/api/tools");
        tools.ShouldContain(t => t.Name == "home-lab__shout" && t.Risk == Orbs.Application.Abstractions.ToolRisk.Read);
        tools.ShouldContain(t => t.Name == "home-lab__turn_on" && t.Risk == Orbs.Application.Abstractions.ToolRisk.Write);
        _factory.McpClient.Connections.Last().Authorization.ShouldBe("Bearer secret-token");
    }

    [Fact]
    public async Task An_orb_can_call_an_mcp_tool()
    {
        await _api.ConnectEchoAsync();
        await ConnectAsync("Lab", "https://mcp.example.com/mcp");
        var orb = await _api.SetToolAccessAsync(await _api.CreateOrbAsync(new SaveOrbRequest("Pip")), "lab__shout", ToolAccess.Allow);
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, """/tool lab__shout {"text":"hi"}""");

        result.Reply.ShouldBe("""lab__shout said: {"TEXT":"HI"}""");
    }

    [Fact]
    public async Task An_unreachable_server_reports_its_error_and_offers_nothing()
    {
        var server = await ConnectAsync("Broken", "https://broken.example.com/mcp");

        var listing = await _api.GetAsync<McpServerToolsDto>($"/api/mcp-servers/{server.Id}/tools");

        listing.Error.ShouldBe("connection refused");
        (await _api.GetAsync<List<McpServerDto>>("/api/mcp-servers")).Single().LastError.ShouldBe("connection refused");
        (await _api.GetAsync<List<ToolDto>>("/api/tools")).ShouldNotContain(t => t.Name.StartsWith("broken__", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Local_command_servers_are_off_unless_an_admin_turns_them_on()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/mcp-servers", new SaveMcpServerRequest("Everything", McpTransport.Stdio, "npx -y server-everything"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admins_reach_their_network_but_other_people_go_through_the_network_policy()
    {
        await ConnectAsync("Mine", "https://mcp.example.com/mcp");
        _ = await _api.GetAsync<List<ToolDto>>("/api/tools");
        _factory.McpClient.Connections.Last().RestrictNetwork.ShouldBeFalse();

        var sam = new ApiClient(await _factory.CreateClientForNewUserAsync("sam@orbs.test"));
        await sam.Http.PostAsJsonAsync("/api/mcp-servers", new SaveMcpServerRequest("Sams", McpTransport.Http, "https://mcp.example.com/mcp"), ApiClient.Json);
        _ = await sam.GetAsync<List<ToolDto>>("/api/tools");
        _factory.McpClient.Connections.Last().RestrictNetwork.ShouldBeTrue();
    }
}

/// <summary>The real MCP client against a real MCP server, hosted in this test on a free local port.</summary>
public sealed class McpProtocolTests : IAsyncLifetime
{
    private WebApplication? _server;
    private Uri? _endpoint;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<TestTools>();
        _server = builder.Build();
        _server.MapMcp("/mcp");
        await _server.StartAsync();
        var address = _server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _endpoint = new Uri(address.TrimEnd('/') + "/mcp");
    }

    public async Task DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tools_are_listed_and_called_over_streamable_http()
    {
        var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var adapter = new McpClientAdapter(services.GetRequiredService<IHttpClientFactory>());
        var connection = new McpConnection(Guid.NewGuid(), McpTransport.Http, _endpoint!.ToString(), null, RestrictNetwork: false);

        var tools = await adapter.ListToolsAsync(connection);
        var add = tools.ShouldHaveSingleItem();
        add.Name.ShouldBe("add");
        add.InputSchema.ShouldContain("\"a\"");

        (await adapter.CallToolAsync(connection, "add", """{"a":2,"b":40}""")).ShouldBe("42");
    }

    [McpServerToolType]
    private sealed class TestTools
    {
        [McpServerTool(Name = "add"), Description("Adds two numbers.")]
        public static int Add(int a, int b) => a + b;
    }
}
