using Orbs.Application.Abstractions;
using Orbs.Application.Conversations;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Tests;

public class PromptBuilderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Orb Pip()
    {
        var orb = Orb.Create("Pip", Now);
        orb.Describe("Work sidekick", Now);
        orb.Brief("Lead with the answer.", Now);
        return orb;
    }

    private static ContextSource Note(string name, string content)
    {
        var source = ContextSource.Create(name, "text", Now);
        source.MarkSynced(content, Now);
        return source;
    }

    [Fact]
    public void The_system_prompt_introduces_the_orb_and_its_brief()
    {
        var turns = PromptBuilder.Build(Pip(), [], []);

        turns[0].Role.ShouldBe(ChatRole.System);
        turns[0].Content.ShouldContain("You are Pip");
        turns[0].Content.ShouldContain("Work sidekick.");
        turns[0].Content.ShouldContain("Lead with the answer.");
    }

    [Fact]
    public void Only_ready_sources_reach_the_model()
    {
        var pending = ContextSource.Create("Pending", "url", Now);
        var turns = PromptBuilder.Build(Pip(), [Note("Roadmap", "Ship billing in Q1"), pending], []);

        turns[0].Content.ShouldContain("### Roadmap (text)");
        turns[0].Content.ShouldContain("Ship billing in Q1");
        turns[0].Content.ShouldNotContain("Pending");
    }

    [Fact]
    public void Context_is_shared_fairly_within_the_budget()
    {
        var budget = new PromptBudget(MaxContextCharacters: 20);
        var turns = PromptBuilder.Build(Pip(), [Note("A", new string('a', 50)), Note("B", new string('b', 50))], [], budget);

        turns[0].Content.ShouldContain(new string('a', 10) + " [truncated]");
        turns[0].Content.ShouldNotContain(new string('a', 11));
    }

    [Fact]
    public void Without_context_there_is_no_knowledge_section()
    {
        PromptBuilder.Build(Pip(), [], [])[0].Content.ShouldNotContain("What you know");
    }

    [Fact]
    public void History_follows_the_system_prompt_and_is_capped()
    {
        var chat = Conversation.Start(Guid.NewGuid(), Now);
        for (var i = 0; i < 5; i++)
        {
            chat.AddUserMessage($"q{i}", Now.AddSeconds(i * 2));
            chat.AddAssistantMessage($"a{i}", Now.AddSeconds((i * 2) + 1));
        }

        var turns = PromptBuilder.Build(Pip(), [], chat.Messages, new PromptBudget(MaxHistoryMessages: 3));

        turns.Count.ShouldBe(4);
        turns.Skip(1).Select(t => t.Content).ShouldBe(["a3", "q4", "a4"]);
        turns[^1].Role.ShouldBe(ChatRole.Assistant);
    }
}
