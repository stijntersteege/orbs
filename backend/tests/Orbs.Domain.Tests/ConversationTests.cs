using Orbs.Domain.Common;
using Orbs.Domain.Conversations;

namespace Orbs.Domain.Tests;

public class ConversationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void The_first_user_message_names_the_chat()
    {
        var chat = Conversation.Start(Guid.NewGuid(), Now);

        chat.AddUserMessage("What is blocking Q1?\nAnd why?", Now);

        chat.Title.ShouldBe("What is blocking Q1?");
    }

    [Fact]
    public void Later_messages_keep_the_title()
    {
        var chat = Conversation.Start(Guid.NewGuid(), Now);
        chat.AddUserMessage("First", Now);

        chat.AddUserMessage("Second", Now);

        chat.Title.ShouldBe("First");
    }

    [Fact]
    public void Long_first_messages_make_a_shortened_title()
    {
        var chat = Conversation.Start(Guid.NewGuid(), Now);

        chat.AddUserMessage(new string('a', 100), Now);

        chat.Title.Length.ShouldBe(60);
        chat.Title.ShouldEndWith("...");
    }

    [Fact]
    public void Empty_messages_are_rejected()
    {
        var chat = Conversation.Start(Guid.NewGuid(), Now);

        Should.Throw<DomainException>(() => chat.AddUserMessage("  ", Now));
    }

    [Fact]
    public void Messages_keep_their_order_and_roles()
    {
        var chat = Conversation.Start(Guid.NewGuid(), Now);

        chat.AddUserMessage("Hi", Now);
        chat.AddAssistantMessage("Hello!", Now.AddSeconds(1));

        chat.Messages.Select(m => m.Role).ShouldBe([MessageRole.User, MessageRole.Assistant]);
        chat.UpdatedAt.ShouldBe(Now.AddSeconds(1));
    }

    [Fact]
    public void A_conversation_needs_an_orb()
    {
        Should.Throw<DomainException>(() => Conversation.Start(Guid.Empty, Now));
    }
}
