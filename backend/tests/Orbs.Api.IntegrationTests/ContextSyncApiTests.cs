using System.Net.Http.Json;
using Orbs.Application.Context;
using Orbs.Application.Jobs;
using Orbs.Domain.Context;
using Orbs.Domain.Jobs;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ContextSyncApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ContextSyncApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_web_page_kept_in_sync_is_read_again_when_due()
    {
        var page = await ApiClient.ReadAsync<ContextSourceDto>(await _api.Http.PostAsJsonAsync("/api/context-sources",
            new SaveContextSourceRequest("Status", "url", Location: "http://127.0.0.1:9/status", SyncEvery: SyncInterval.Hourly), ApiClient.Json));
        page.SyncEvery.ShouldBe(SyncInterval.Hourly);
        page.NextSyncAt.ShouldNotBeNull();

        await _factory.RunSchedulesAsync();
        (await _api.GetAsync<List<JobDto>>("/api/jobs")).ShouldNotContain(j => j.Kind == ContextSyncJob.KindName);

        _factory.Clock.Advance(TimeSpan.FromHours(1));
        await _factory.RunSchedulesAsync();

        var job = (await _api.GetAsync<List<JobDto>>("/api/jobs")).Single(j => j.Kind == ContextSyncJob.KindName);
        job.LastError.ShouldNotBeNull().ShouldContain("private networks", Case.Insensitive, "the page itself is unreachable, but the sync ran");
        (await _api.GetAsync<ContextSourceDto>($"/api/context-sources/{page.Id}")).NextSyncAt.ShouldNotBeNull().ShouldBeGreaterThan(page.NextSyncAt!.Value);
    }

    [Fact]
    public async Task Notes_never_sync_in_the_background()
    {
        var note = await ApiClient.ReadAsync<ContextSourceDto>(await _api.Http.PostAsJsonAsync("/api/context-sources",
            new SaveContextSourceRequest("Note", "text", Input: "Hi", SyncEvery: SyncInterval.Daily), ApiClient.Json));

        note.SyncEvery.ShouldBe(SyncInterval.Off);
        note.NextSyncAt.ShouldBeNull();
    }
}
