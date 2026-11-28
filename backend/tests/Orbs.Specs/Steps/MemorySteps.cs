using System.Net.Http.Json;
using Orbs.Application.Memories;
using Orbs.Specs.Support;
using Orbs.Testing;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class MemorySteps(SpecContext context)
{
    [Given("{string} remembers that {string}")]
    public async Task GivenRemembers(string orb, string fact) =>
        await ApiClient.ReadAsync<MemoryDto>(await context.Api.Http.PostAsJsonAsync($"/api/orbs/{context.Orbs[orb].Id}/memories", new SaveMemoryRequest(fact), ApiClient.Json));

    [Given("{string} remembers for all orbs that {string}")]
    public async Task GivenRemembersForAll(string orb, string fact) =>
        await ApiClient.ReadAsync<MemoryDto>(await context.Api.Http.PostAsJsonAsync($"/api/orbs/{context.Orbs[orb].Id}/memories", new SaveMemoryRequest(fact, Shared: true), ApiClient.Json));

    [When("I forget the memory {string}")]
    public async Task WhenIForget(string fact)
    {
        foreach (var orb in context.Orbs.Values)
        {
            var match = (await List(orb.Id)).FirstOrDefault(m => m.Content == fact);
            if (match is not null)
            {
                (await context.Api.Http.DeleteAsync(new Uri($"/api/memories/{match.Id}", UriKind.Relative))).EnsureSuccessStatusCode();
            }
        }
    }

    [Then("{string} remembers {string}")]
    public async Task ThenRemembers(string orb, string fact) =>
        (await List(context.Orbs[orb].Id)).Select(m => m.Content).ShouldContain(fact);

    [Then("{string} remembers nothing")]
    public async Task ThenRemembersNothing(string orb) =>
        (await List(context.Orbs[orb].Id)).ShouldBeEmpty();

    private Task<List<MemoryDto>> List(Guid orbId) => context.Api.GetAsync<List<MemoryDto>>($"/api/orbs/{orbId}/memories");
}
