using System.Collections.Concurrent;
using Orbs.Application.Channels;
using Orbs.Application.Tools;

namespace Orbs.Testing;

/// <summary>A messaging app in memory: tests put messages in the inbox and read the orb's answers from the outbox.</summary>
public sealed class FakeChannel : IChannelAdapter
{
    public const string KindName = "test-chat";

    public ConcurrentQueue<ChannelMessage> Inbox { get; } = new();

    public List<(string ChatId, string Text)> Outbox { get; } = [];

    public string Kind => KindName;

    public string DisplayName => "Test chat";

    public string Description => "An in-memory chat app for tests.";

    public IReadOnlyList<IntegrationField> Fields { get; } = [new("token", "Token", Required: true, Secret: true)];

    public Task<string> ConnectAsync(ChannelConfig config, CancellationToken cancellationToken = default) =>
        config.Secret == "bad" ? throw new HttpRequestException("Unauthorized") : Task.FromResult("@test_bot");

    public Task<ChannelPoll> PollAsync(ChannelConfig config, string? cursor, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChannelMessage>();
        while (Inbox.TryDequeue(out var message))
        {
            messages.Add(message);
        }

        return Task.FromResult(new ChannelPoll(messages, cursor));
    }

    public Task SendAsync(ChannelConfig config, string chatId, string text, CancellationToken cancellationToken = default)
    {
        Outbox.Add((chatId, text));
        return Task.CompletedTask;
    }

    public Task<string?> SetProfileAsync(ChannelConfig config, string name, string description, byte[]? avatarPng, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
