using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Orbs;
using Orbs.Application.Schedules;
using Orbs.Domain.Orbs;
using Orbs.Domain.Schedules;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class OrbTemplateTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public OrbTemplateTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_exported_orb_imports_for_someone_else_with_its_schedules()
    {
        var chef = await _api.CreateOrbAsync(new SaveOrbRequest(
            "Chef",
            "Knows recipes",
            "Answer like a chef.",
            ToolPermissions: new Dictionary<string, ToolAccess> { ["calculator"] = ToolAccess.Allow },
            Voice: "alloy"));
        await Post("/api/schedules", new SaveScheduleRequest(chef.Id, "Menu", "Plan the week's menu", TriggerKind.Time, "0 9 * * 1", "Europe/Amsterdam"));
        await Post("/api/schedules", new SaveScheduleRequest(chef.Id, "Doorbell", "Who rang?", TriggerKind.Webhook));

        var template = await _api.GetAsync<OrbTemplate>($"/api/orbs/{chef.Id}/template");
        template.Format.ShouldBe("orbs-template");
        template.Schedules.ShouldNotBeNull().Select(s => s.Name).ShouldBe(["Menu"], "webhook schedules belong to one account");

        var sam = new ApiClient(await _factory.CreateClientForNewUserAsync("sam@orbs.test"));
        var result = await ApiClient.ReadAsync<OrbImportResult>(await sam.Http.PostAsJsonAsync("/api/orbs/import", template, ApiClient.Json));

        result.Schedules.ShouldBe(1);
        result.Orb.Name.ShouldBe("Chef");
        result.Orb.Instructions.ShouldBe("Answer like a chef.");
        result.Orb.Appearance.ShouldBe(chef.Appearance);
        result.Orb.Voice.ShouldBe("alloy");
        result.Orb.ToolPermissions.ShouldNotBeNull()["calculator"].ShouldBe(ToolAccess.Allow);
        var schedule = (await sam.GetAsync<List<ScheduleDto>>("/api/schedules")).ShouldHaveSingleItem();
        schedule.OrbId.ShouldBe(result.Orb.Id);
        schedule.Cron.ShouldBe("0 9 * * 1");
    }

    [Fact]
    public async Task Importing_next_to_an_orb_with_the_same_name_numbers_the_new_one()
    {
        var pip = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var template = await _api.GetAsync<OrbTemplate>($"/api/orbs/{pip.Id}/template");

        var result = await ApiClient.ReadAsync<OrbImportResult>(await _api.Http.PostAsJsonAsync("/api/orbs/import", template, ApiClient.Json));

        result.Orb.Name.ShouldBe("Pip 2");
    }

    [Fact]
    public async Task Something_that_is_not_a_template_is_refused()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/orbs/import", new OrbTemplate("other", 1, new OrbTemplateOrb("X")), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _api.GetAsync<List<OrbDto>>("/api/orbs")).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_template_with_a_broken_schedule_creates_nothing()
    {
        var template = new OrbTemplate("orbs-template", 1, new OrbTemplateOrb("Chef"), [new OrbTemplateSchedule("Bad", "x", TriggerKind.Time, "not cron")]);

        var response = await _api.Http.PostAsJsonAsync("/api/orbs/import", template, ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _api.GetAsync<List<OrbDto>>("/api/orbs")).ShouldBeEmpty();
    }

    private async Task Post<T>(string path, T body) =>
        (await _api.Http.PostAsJsonAsync(path, body, ApiClient.Json)).EnsureSuccessStatusCode();
}
