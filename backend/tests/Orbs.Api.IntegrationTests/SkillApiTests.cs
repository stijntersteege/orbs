using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Application.Skills;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class SkillApiTests : IDisposable
{
    private const string WeeklyReview = """
        ---
        name: Weekly Review
        description: Sum up the week
        ---
        1. Check the calendar.
        """;

    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public SkillApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_skill_file_imports_and_exports_again()
    {
        var skill = await ImportAsync(WeeklyReview);
        skill.Name.ShouldBe("weekly-review");
        skill.Instructions.ShouldBe("1. Check the calendar.");

        var response = await _api.Http.GetAsync(new Uri($"/api/skills/{skill.Id}/export", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileName.ShouldBe("weekly-review.SKILL.md");
        (await response.Content.ReadAsStringAsync()).ShouldBe("---\nname: weekly-review\ndescription: \"Sum up the week\"\n---\n\n1. Check the calendar.\n");
    }

    [Fact]
    public async Task Importing_the_same_name_again_replaces_the_skill()
    {
        await ImportAsync(WeeklyReview);
        await ImportAsync(WeeklyReview.Replace("Check the calendar", "Read the inbox", StringComparison.Ordinal));

        var skill = (await _api.GetAsync<List<SkillDto>>("/api/skills")).ShouldHaveSingleItem();
        skill.Instructions.ShouldBe("1. Read the inbox.");
    }

    [Fact]
    public async Task Two_skills_cannot_share_a_name()
    {
        await ImportAsync(WeeklyReview);

        var response = await _api.Http.PostAsJsonAsync("/api/skills", new SaveSkillRequest("weekly-review", "Other", "Steps"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_orb_reads_a_skill_without_asking()
    {
        await _api.ConnectEchoAsync();
        await ImportAsync(WeeklyReview);
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var listed = await _api.SendAsync(chat.Id, "/tool use_skill {}");
        var used = await _api.SendAsync(chat.Id, """/tool use_skill {"skill":"Weekly Review"}""");

        listed.Reply.ShouldBe("use_skill said: Skills:\n- weekly-review: Sum up the week");
        used.Events.ShouldNotContain(e => e.Type == ChatEvent.ApprovalType);
        used.Reply.ShouldBe("use_skill said: Skill \"weekly-review\": Sum up the week\n\n1. Check the calendar.");
    }

    [Fact]
    public async Task A_reply_that_used_tools_becomes_a_skill()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.SetToolAccessAsync(await _api.CreateOrbAsync(new SaveOrbRequest("Pip")), "calculator", Orbs.Domain.Orbs.ToolAccess.Allow);
        var chat = await _api.StartConversationAsync(orb.Id);
        var result = await _api.SendAsync(chat.Id, """/tool calculator {"expression":"6*7"}""");
        var messageId = result.Events.Single(e => e.Type == ChatEvent.DoneType).MessageId;

        var response = await _api.Http.PostAsJsonAsync($"/api/conversations/{chat.Id}/messages/{messageId}/skill", new SkillFromReplyRequest("Do the math"), ApiClient.Json);

        var skill = await ApiClient.ReadAsync<SkillDto>(response);
        skill.Name.ShouldBe("do-the-math");
        skill.Description.ShouldContain("Repeats the steps from the chat");
        skill.Instructions.ShouldContain("1. Use the `calculator` tool with arguments like `{\"expression\":\"6*7\"}`.");
    }

    [Fact]
    public async Task A_reply_without_tools_cannot_become_a_skill()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);
        var result = await _api.SendAsync(chat.Id, "hello");
        var messageId = result.Events.Single(e => e.Type == ChatEvent.DoneType).MessageId;

        var response = await _api.Http.PostAsJsonAsync($"/api/conversations/{chat.Id}/messages/{messageId}/skill", new SkillFromReplyRequest("nothing"), ApiClient.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Other_people_cannot_see_your_skills()
    {
        await ImportAsync(WeeklyReview);
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");

        (await new ApiClient(sam).GetAsync<List<SkillDto>>("/api/skills")).ShouldBeEmpty();
    }

    private async Task<SkillDto> ImportAsync(string markdown) =>
        await ApiClient.ReadAsync<SkillDto>(await _api.Http.PostAsJsonAsync("/api/skills/import", new ImportSkillRequest(markdown), ApiClient.Json));
}
