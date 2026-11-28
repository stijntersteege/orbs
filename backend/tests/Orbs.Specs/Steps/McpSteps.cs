using System.Net.Http.Json;
using Orbs.Application.Tools;
using Orbs.Domain.Tools;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class McpSteps(SpecContext context)
{
    [Given("I connect the MCP server {string} at {string}")]
    [When("I connect the MCP server {string} at {string}")]
    public async Task ConnectServer(string name, string url) =>
        await ApiClient.ReadAsync<McpServerDto>(
            await context.Api.Http.PostAsJsonAsync("/api/mcp-servers", new SaveMcpServerRequest(name, McpTransport.Http, url), ApiClient.Json));

    [Then("the tool {string} is available")]
    public async Task ThenTheToolIsAvailable(string name) =>
        (await context.Api.GetAsync<List<ToolDto>>("/api/tools")).Select(t => t.Name).ShouldContain(name);
}
