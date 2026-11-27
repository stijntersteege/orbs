using Orbs.Application.Context;

namespace Orbs.Application.Tests;

public class RetrievalTests
{
    [Fact]
    public void Short_text_is_one_passage()
    {
        Chunker.Split("Just a note.").ShouldBe(["Just a note."]);
    }

    [Fact]
    public void Long_text_is_split_into_overlapping_passages_on_paragraph_boundaries()
    {
        var paragraphs = Enumerable.Range(1, 30).Select(i => $"Paragraph {i}: " + new string('x', 80)).ToList();
        var text = string.Join("\n\n", paragraphs);

        var chunks = Chunker.Split(text);

        chunks.Count.ShouldBeGreaterThan(2);
        chunks.ShouldAllBe(c => c.Length <= Chunker.Size);
        chunks[0].ShouldStartWith("Paragraph 1:");
        chunks[^1].ShouldEndWith(new string('x', 80));
        string.Concat(chunks).ShouldContain("Paragraph 30:");
    }

    [Fact]
    public void Cosine_similarity_compares_directions()
    {
        Chunker.Cosine([1, 0], [2, 0]).ShouldBe(1f, 0.0001f);
        Chunker.Cosine([1, 0], [0, 3]).ShouldBe(0f, 0.0001f);
        Chunker.Cosine([1, 0], [1, 0, 0]).ShouldBe(0f);
    }

    [Fact]
    public void Passages_are_joined_with_a_gap_marker()
    {
        ContextRetriever.Join(["one", "two"]).ShouldBe("one\n[...]\ntwo");
    }
}
