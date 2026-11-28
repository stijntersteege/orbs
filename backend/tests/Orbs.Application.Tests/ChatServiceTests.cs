using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Common;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Memories;
using Orbs.Application.Providers;
using Orbs.Application.Tests.Fakes;
using Orbs.Application.Tools;
using Orbs.Domain.Context;
using Orbs.Domain.Conversations;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Application.Tests;

public class ChatServiceTests
{
    private readonly InMemoryStore _store = new();
    private readonly FakeClock _clock = FakeClock.Default;

    private ChatService CreateService(params IModelAdapter[] adapters)
    {
        var registry = new PluginRegistry<IModelAdapter>(adapters);
        var providerService = new ProviderService(_store.Providers, _store.Orbs, registry, new FakeSecretProtector(), _store, _clock);
        var tools = new ToolRunner(new ToolCatalog([]), new CurrentUser());
        var engine = new ReplyEngine(tools, _store.Approvals, _store, _clock);
        var memories = new MemoryService(_store.Memories, _store.Orbs, _store, _clock);
        var retriever = new ContextRetriever(_store.Chunks, new EmbedderResolver(_store.Providers, registry, providerService));
        var embedders = new EmbedderResolver(_store.Providers, registry, providerService);
        var indexer = new ContextIndexer(embedders, new global::Orbs.Application.Jobs.JobScheduler(_store.Jobs, _store, _clock), new CurrentUser());
        var files = new FileContextService(_store.Context, _store.Files, new NoDocuments(), indexer, _store, _clock);
        return new ChatService(_store.Conversations, _store.Orbs, _store.Providers, _store.Context, registry, providerService, tools, engine, _store.Approvals, memories, retriever, files, _store, _clock);
    }

    private (Orb Orb, Conversation Chat) GivenAnOrbWithAChat(string providerKind = "scripted")
    {
        var provider = ModelProvider.Create("Scripted", providerKind, _clock.Now);
        provider.SetDefaultModel("model-a", _clock.Now);
        provider.SetProtectedApiKey("protected:secret", _clock.Now);
        _store.ProviderItems.Add(provider);

        var orb = Orb.Create("Pip", _clock.Now);
        _store.OrbItems.Add(orb);

        var chat = Conversation.Start(orb.Id, _clock.Now);
        _store.ConversationItems.Add(chat);
        return (orb, chat);
    }

    private static async Task<List<ChatEvent>> CollectAsync(IAsyncEnumerable<ChatEvent> events)
    {
        var list = new List<ChatEvent>();
        await foreach (var e in events)
        {
            list.Add(e);
        }

        return list;
    }

    [Fact]
    public async Task A_reply_is_streamed_and_then_stored()
    {
        var adapter = new ScriptedModelAdapter("scripted", "Hello", " there");
        var (_, chat) = GivenAnOrbWithAChat();

        var events = await CollectAsync(await CreateService(adapter).SendAsync(chat.Id, new SendMessageRequest("Hi")));

        events.Select(e => e.Type).ShouldBe(["start", "delta", "delta", "done"]);
        events[0].Model.ShouldBe("model-a");
        chat.Messages.Select(m => (m.Role, m.Content)).ShouldBe([(MessageRole.User, "Hi"), (MessageRole.Assistant, "Hello there")]);
        events[^1].MessageId.ShouldBe(chat.Messages[^1].Id);
    }

    [Fact]
    public async Task The_adapter_gets_the_decrypted_key_and_the_orbs_context()
    {
        var adapter = new ScriptedModelAdapter("scripted", "ok");
        var (orb, chat) = GivenAnOrbWithAChat();
        var note = ContextSource.Create("Roadmap", "text", _clock.Now);
        note.MarkSynced("Billing ships in Q1", _clock.Now);
        _store.ContextItems.Add(note);
        orb.LinkContext(note.Id, _clock.Now);

        await CollectAsync(await CreateService(adapter).SendAsync(chat.Id, new SendMessageRequest("When does billing ship?")));

        adapter.LastConnection!.ApiKey.ShouldBe("secret");
        adapter.LastRequest!.Messages[0].Content.ShouldContain("Billing ships in Q1");
        adapter.LastRequest.Messages[^1].Content.ShouldBe("When does billing ship?");
    }

    [Fact]
    public async Task The_start_event_says_which_provider_answers_and_which_sources_it_reads()
    {
        var adapter = new ScriptedModelAdapter("scripted", "ok");
        var (orb, chat) = GivenAnOrbWithAChat();
        var note = ContextSource.Create("Roadmap", "text", _clock.Now);
        note.MarkSynced("Billing ships in Q1", _clock.Now);
        var empty = ContextSource.Create("Empty page", "url", _clock.Now);
        _store.ContextItems.Add(note);
        _store.ContextItems.Add(empty);
        orb.LinkContext(note.Id, _clock.Now);
        orb.LinkContext(empty.Id, _clock.Now);

        var events = await CollectAsync(await CreateService(adapter).SendAsync(chat.Id, new SendMessageRequest("When does billing ship?")));

        events[0].Provider.ShouldNotBeNullOrEmpty();
        var source = events[0].Sources.ShouldHaveSingleItem();
        source.Id.ShouldBe(note.Id);
        source.Name.ShouldBe("Roadmap");
        source.Characters.ShouldBe("Billing ships in Q1".Length);
        source.Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task A_failing_provider_ends_with_an_error_and_stores_no_reply()
    {
        var adapter = new ScriptedModelAdapter("scripted", "Part", "never") { FailAfterFirstFragment = new HttpRequestException("boom") };
        var (_, chat) = GivenAnOrbWithAChat();

        var events = await CollectAsync(await CreateService(adapter).SendAsync(chat.Id, new SendMessageRequest("Hi")));

        events.Select(e => e.Type).ShouldBe(["start", "delta", "error"]);
        events[^1].Text!.ShouldContain("boom");
        chat.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Without_a_model_the_message_is_refused_before_it_is_stored()
    {
        var orb = Orb.Create("Pip", _clock.Now);
        _store.OrbItems.Add(orb);
        var chat = Conversation.Start(orb.Id, _clock.Now);
        _store.ConversationItems.Add(chat);

        await Should.ThrowAsync<ModelUnavailableException>(() =>
            CreateService(new ScriptedModelAdapter()).SendAsync(chat.Id, new SendMessageRequest("Hi")));
        chat.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Starting_a_chat_with_a_missing_orb_is_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() =>
            CreateService(new ScriptedModelAdapter()).StartAsync(new StartConversationRequest(Guid.NewGuid())));
    }
}

internal sealed class NoDocuments : IDocumentReader
{
    public string ContentTypeFor(string fileName, string? declared) => declared ?? "application/octet-stream";

    public bool CanRead(string contentType) => false;

    public Task<string> ReadTextAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);
}
