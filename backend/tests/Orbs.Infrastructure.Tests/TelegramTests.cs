using System.Text.Json;
using Orbs.Infrastructure.Channels;

namespace Orbs.Infrastructure.Tests;

public class TelegramTests
{
    [Fact]
    public void Updates_become_messages_and_the_cursor_moves_past_the_last_one()
    {
        var root = JsonElement.Parse("""
            {"ok":true,"result":[
              {"update_id":41,"message":{"chat":{"id":1001},"from":{"first_name":"Sam"},"text":"/start K7MPQ2"}},
              {"update_id":42,"message":{"chat":{"id":1001},"from":{"username":"sam"},"sticker":{}}},
              {"update_id":43,"message":{"chat":{"id":-5},"text":"hi"}}
            ]}
            """);

        var poll = TelegramChannel.ParseUpdates(root, "40");

        poll.Cursor.ShouldBe("44");
        poll.Messages.Select(m => (m.ChatId, m.SenderName, m.Text)).ShouldBe([("1001", "Sam", "/start K7MPQ2"), ("-5", "someone", "hi")]);
    }

    [Fact]
    public void An_empty_poll_keeps_the_cursor()
    {
        TelegramChannel.ParseUpdates(JsonElement.Parse("""{"ok":true,"result":[]}"""), "40").Cursor.ShouldBe("40");
    }

    [Fact]
    public void Long_replies_are_split_on_paragraphs()
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => new string((char)('a' + i), 900)));

        var parts = TelegramChannel.Split(text, TelegramChannel.MaxMessageLength).ToList();

        parts.Count.ShouldBe(3);
        parts.ShouldAllBe(p => p.Length <= TelegramChannel.MaxMessageLength);
        string.Concat(parts).Replace("\n", string.Empty, StringComparison.Ordinal).Length.ShouldBe(9_000);
    }
}
