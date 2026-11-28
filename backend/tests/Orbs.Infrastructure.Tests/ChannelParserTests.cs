using System.Text.Json;
using MimeKit;
using Orbs.Infrastructure.Channels;

namespace Orbs.Infrastructure.Tests;

public class ChannelParserTests
{
    [Fact]
    public void Matrix_sync_yields_text_from_others_and_rooms_to_join()
    {
        var root = JsonElement.Parse("""
            {"next_batch":"s42","rooms":{
              "invite":{"!new:example.org":{}},
              "join":{"!room:example.org":{"timeline":{"events":[
                {"type":"m.room.message","sender":"@sam:example.org","content":{"msgtype":"m.text","body":"Hi Pip"}},
                {"type":"m.room.message","sender":"@pip:example.org","content":{"msgtype":"m.text","body":"my own echo"}},
                {"type":"m.room.message","sender":"@sam:example.org","content":{"msgtype":"m.image","body":"pic.png"}},
                {"type":"m.reaction","sender":"@sam:example.org","content":{}}
              ]}}}}}
            """);

        var (poll, invites) = MatrixChannel.ParseSync(root, "@pip:example.org", firstSync: false);

        poll.Cursor.ShouldBe("s42");
        poll.Messages.ShouldHaveSingleItem().ShouldBe(new Orbs.Application.Channels.ChannelMessage("!room:example.org", "@sam:example.org", "Hi Pip"));
        invites.ShouldBe(["!new:example.org"]);
    }

    [Fact]
    public void The_first_matrix_sync_does_not_answer_old_history()
    {
        var root = JsonElement.Parse("""{"next_batch":"s1","rooms":{"join":{"!r":{"timeline":{"events":[{"type":"m.room.message","sender":"@sam:x","content":{"msgtype":"m.text","body":"old"}}]}}}}}""");

        MatrixChannel.ParseSync(root, "@pip:x", firstSync: true).Poll.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void Discord_skips_bots_and_starts_from_the_newest_message()
    {
        var root = JsonElement.Parse("""
            [{"id":"300","content":"second","author":{"username":"sam","global_name":"Sam"}},
             {"id":"200","content":"from a bot","author":{"username":"pip","bot":true}},
             {"id":"100","content":"first","author":{"username":"sam"}}]
            """);

        DiscordChannel.ParseMessages(root, "c1", cursor: null).ShouldBe(new Orbs.Application.Channels.ChannelPoll([], "300"));

        var poll = DiscordChannel.ParseMessages(root, "c1", cursor: "50");
        poll.Messages.Select(m => m.Text).ShouldBe(["first", "second"]);
        poll.Messages[1].SenderName.ShouldBe("Sam");
        poll.Cursor.ShouldBe("300");
    }

    [Fact]
    public void Signal_keeps_direct_text_messages_only()
    {
        var root = JsonElement.Parse("""
            [{"envelope":{"sourceNumber":"+31600000001","sourceName":"Sam","dataMessage":{"message":"Hello"}}},
             {"envelope":{"sourceNumber":"+31600000001","receiptMessage":{}}},
             {"envelope":{"sourceNumber":"+31600000002","dataMessage":{"message":"group chat","groupInfo":{"groupId":"g"}}}}]
            """);

        SignalChannel.ParseReceived(root).ShouldBe([new Orbs.Application.Channels.ChannelMessage("+31600000001", "Sam", "Hello")]);
    }

    [Fact]
    public void Email_bodies_drop_quotes_and_signatures()
    {
        var mail = new MimeMessage { Subject = "Dinner" };
        mail.Body = new TextPart("plain") { Text = "Shall we cook pasta?\n\nOn Mon, Pip wrote:\n> earlier text\n-- \nSam's signature" };

        EmailChannel.Body(mail).ShouldBe("Dinner\n\nShall we cook pasta?");
    }
}
