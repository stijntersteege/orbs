using System.Net;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class RetrievalApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public RetrievalApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private static string LongNote(string topic, string filler) =>
        string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"Paragraph {i} about {filler} and nothing else of note, repeated for length.")) +
        $"\n\nThe {topic} is kept in the blue cabinet next to the window in the hallway.";

    [Fact]
    public async Task Indexed_sources_send_only_the_passages_that_match_the_question()
    {
        await _api.CreateProviderAsync(new SaveProviderRequest("Echo", "echo", DefaultModel: "echo-1", EmbeddingModel: "echo-embed"));
        var manual = await _api.AddNoteAsync("House manual", LongNote("spare key", "gardening"));
        var recipes = await _api.AddNoteAsync("Recipes", "Pancakes need flour, eggs and milk. Stir well, rest the batter, fry in butter.");
        (await _factory.RunJobsAsync()).ShouldBe(2);

        var indexed = await _api.GetAsync<ContextSourceDto>($"/api/context-sources/{manual.Id}");
        indexed.IndexedChunks.ShouldBeGreaterThan(1);

        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip", ContextSourceIds: [manual.Id, recipes.Id]));
        var chat = await _api.StartConversationAsync(orb.Id);
        var start = (await _api.SendAsync(chat.Id, "Where is the spare key kept in the cabinet?")).Events[0];

        var used = start.Sources.ShouldNotBeNull().Single(s => s.Name == "House manual");
        used.Passages.ShouldNotBeNull().ShouldBeInRange(1, indexed.IndexedChunks - 1);
        used.Characters.ShouldBeLessThan(LongNote("spare key", "gardening").Length);
        start.Sources.ShouldNotContain(s => s.Name == "Recipes", "nothing in the recipes matches the question");
    }

    [Fact]
    public async Task Without_an_embedding_model_sources_use_the_character_budget()
    {
        await _api.ConnectEchoAsync();
        var note = await _api.AddNoteAsync("Note", "Sam leads design.");

        (await _factory.RunJobsAsync()).ShouldBe(0);
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip", ContextSourceIds: [note.Id]));
        var chat = await _api.StartConversationAsync(orb.Id);

        var source = (await _api.SendAsync(chat.Id, "Who leads design?")).Events[0].Sources.ShouldNotBeNull().Single();
        source.Passages.ShouldBeNull();
        source.Characters.ShouldBe("Sam leads design.".Length);
    }

    [Fact]
    public async Task Indexing_needs_an_embedding_model()
    {
        await _api.ConnectEchoAsync();
        var note = await _api.AddNoteAsync("Note", "Hello");

        var response = await _api.Http.PostAsync(new Uri($"/api/context-sources/{note.Id}/index", UriKind.Relative), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Providers_say_whether_they_can_embed()
    {
        var kinds = await _api.GetAsync<List<ProviderKindDto>>("/api/providers/kinds");

        kinds.Single(k => k.Kind == "openai-compatible").SupportsEmbeddings.ShouldBeTrue();
        kinds.Single(k => k.Kind == "anthropic").SupportsEmbeddings.ShouldBeFalse();
    }
}
