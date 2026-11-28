using Orbs.Application.Providers;
using Orbs.Specs.Support;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class ModelSteps(SpecContext context)
{
    [Then("I can connect a {string} provider without a base URL")]
    public async Task ThenICanConnect(string kind)
    {
        var kinds = await context.Api.GetAsync<List<ProviderKindDto>>("/api/providers/kinds");
        var found = kinds.Single(k => k.Kind == kind);
        found.RequiresBaseUrl.ShouldBeFalse();
        found.RequiresApiKey.ShouldBeTrue();

        var provider = await context.Api.CreateProviderAsync(new SaveProviderRequest(found.DisplayName, kind, ApiKey: "key", DefaultModel: "some-model"));
        provider.HasApiKey.ShouldBeTrue();
    }
}
