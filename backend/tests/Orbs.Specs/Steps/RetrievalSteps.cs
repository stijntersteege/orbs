using Orbs.Application.Providers;
using Orbs.Specs.Support;
using Reqnroll;

namespace Orbs.Specs.Steps;

[Binding]
public sealed class RetrievalSteps(SpecContext context)
{
    [Given("the demo model is connected with embeddings")]
    public Task GivenEchoWithEmbeddings() =>
        context.Api.CreateProviderAsync(new SaveProviderRequest("Echo", "echo", DefaultModel: "echo-1", EmbeddingModel: "echo-embed"));

    [Given("a long note {string} that mentions {string} once")]
    public async Task GivenALongNote(string name, string fact)
    {
        var filler = string.Join("\n\n", Enumerable.Range(1, 15).Select(i => $"Section {i} covers the garden hose, the lawn and the hedges in great detail."));
        context.Sources[name] = await context.Api.AddNoteAsync(name, filler + "\n\nGood to know: " + fact + ".");
    }

    [Then("the reply read passages from {string}")]
    public void ThenTheReplyReadPassages(string name)
    {
        var source = context.LastReply!.Events[0].Sources!.Single(s => s.Name == name);
        source.Passages.ShouldNotBeNull().ShouldBeGreaterThan(0);
    }
}
