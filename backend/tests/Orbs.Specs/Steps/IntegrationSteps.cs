using System.Net.Http.Json;
using Orbs.Application.Tools;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class IntegrationSteps(SpecContext context)
{
    [When("I connect {string} as {string} with the address {string}")]
    public Task WhenIConnect(string kind, string name, string url) =>
        ConnectAsync(new SaveIntegrationRequest(kind, name, new Dictionary<string, string> { ["url"] = url }, "token"));

    [Given("I connect an email account with sending")]
    public Task GivenAnEmailAccount() =>
        ConnectAsync(new SaveIntegrationRequest("email", "Mail", new Dictionary<string, string>
        {
            ["imapHost"] = "imap.example.com",
            ["username"] = "me@example.com",
            ["smtpHost"] = "smtp.example.com",
        }, "app-password"));

    private async Task ConnectAsync(SaveIntegrationRequest request) =>
        await ApiClient.ReadAsync<IntegrationDto>(await context.Api.Http.PostAsJsonAsync("/api/integrations", request, ApiClient.Json));
}
