using Orbs.Application.Conversations;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Tests;

public class MentionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private readonly Orb _pip = Orb.Create("Pip", Now);
    private readonly Orb _nova = Orb.Create("Nova", Now);
    private readonly Orb _lab = Orb.Create("Lab Helper", Now);
    private readonly Orb _lab2 = Orb.Create("Lab", Now);

    private IReadOnlyList<Orb> All => [_pip, _nova, _lab, _lab2];

    [Fact]
    public void Mentions_are_found_in_order_and_once()
    {
        Mentions.Find("@nova what do you think, and @Pip? @Nova again", All).ShouldBe([_nova, _pip]);
    }

    [Fact]
    public void Names_with_spaces_win_over_shorter_names()
    {
        Mentions.Find("Ask @Lab Helper please", All).ShouldBe([_lab]);
        Mentions.Find("Ask @Lab please", All).ShouldBe([_lab2]);
    }

    [Theory]
    [InlineData("mail me at sam@nova.example")]
    [InlineData("@Novas are bright")]
    [InlineData("no mentions here")]
    public void Emails_and_partial_words_are_not_mentions(string text)
    {
        Mentions.Find(text, All).ShouldBeEmpty();
    }

    [Fact]
    public void In_a_group_chat_other_orbs_replies_read_as_their_words()
    {
        var chat = global::Orbs.Domain.Conversations.Conversation.Start(_pip.Id, Now);
        chat.Invite(_nova.Id, Now);
        chat.AddUserMessage("Plan a trip", Now);
        chat.AddAssistantMessage("Lisbon?", Now.AddSeconds(1), orbId: _nova.Id);

        var turns = PromptBuilder.Build(_pip, [], chat.Messages, groupNames: new Dictionary<Guid, string> { [_pip.Id] = "Pip", [_nova.Id] = "Nova" });

        turns[0].Content.ShouldContain("group chat with the person and Nova");
        turns[^1].Role.ShouldBe(global::Orbs.Application.Abstractions.ChatRole.User);
        turns[^1].Content.ShouldBe("Nova said: Lisbon?");
    }
}
