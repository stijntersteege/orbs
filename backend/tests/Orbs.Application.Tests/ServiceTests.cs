using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Common;
using Orbs.Application.Context;
using Orbs.Application.Jobs;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Application.Tests.Fakes;
using Orbs.Domain.Common;
using Orbs.Domain.Context;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Tests;

public class OrbServiceTests
{
    private readonly InMemoryStore _store = new();

    private OrbService Service => new(_store.Orbs, _store.Providers, _store.Context, _store, FakeClock.Default);

    [Fact]
    public async Task Creating_an_orb_saves_it_with_its_look()
    {
        var look = OrbAppearance.Default with { Color = OrbColor.Mint, Eyes = OrbEyes.Visor };

        var orb = await Service.CreateAsync(new SaveOrbRequest("Byte", "Data nerd", Appearance: look));

        orb.Appearance.ShouldBe(look);
        _store.OrbItems.Items.Single().Name.ShouldBe("Byte");
        _store.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task Linking_unknown_context_is_refused()
    {
        await Should.ThrowAsync<DomainException>(() =>
            Service.CreateAsync(new SaveOrbRequest("Byte", ContextSourceIds: [Guid.NewGuid()])));
        _store.OrbItems.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pinning_a_model_of_an_unknown_provider_is_refused()
    {
        await Should.ThrowAsync<DomainException>(() =>
            Service.CreateAsync(new SaveOrbRequest("Byte", Model: new ModelRefDto(Guid.NewGuid(), "m"))));
    }

    [Fact]
    public async Task Updating_a_missing_orb_is_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() => Service.UpdateAsync(Guid.NewGuid(), new SaveOrbRequest("X")));
    }
}

public class ProviderServiceTests
{
    private readonly InMemoryStore _store = new();

    private ProviderService Service(params IModelAdapter[] adapters) =>
        new(_store.Providers, _store.Orbs, new PluginRegistry<IModelAdapter>(adapters), new FakeSecretProtector(), _store, FakeClock.Default, new CurrentUser());

    [Fact]
    public async Task Api_keys_are_stored_encrypted_and_never_returned()
    {
        var dto = await Service(new ScriptedModelAdapter("scripted")).CreateAsync(new SaveProviderRequest("Lab", "scripted", ApiKey: "sk-123"));

        dto.HasApiKey.ShouldBeTrue();
        _store.ProviderItems.Items.Single().ProtectedApiKey.ShouldBe("protected:sk-123");
    }

    [Fact]
    public async Task On_update_a_null_key_keeps_the_key_and_an_empty_key_removes_it()
    {
        var service = Service(new ScriptedModelAdapter("scripted"));
        var dto = await service.CreateAsync(new SaveProviderRequest("Lab", "scripted", ApiKey: "sk-123"));

        (await service.UpdateAsync(dto.Id, new SaveProviderRequest("Lab", "scripted"))).HasApiKey.ShouldBeTrue();
        (await service.UpdateAsync(dto.Id, new SaveProviderRequest("Lab", "scripted", ApiKey: ""))).HasApiKey.ShouldBeFalse();
    }

    [Fact]
    public async Task Required_settings_of_the_adapter_are_enforced()
    {
        var adapter = new ScriptedModelAdapter("scripted") { RequiresBaseUrl = true };

        await Should.ThrowAsync<DomainException>(() => Service(adapter).CreateAsync(new SaveProviderRequest("Lab", "scripted")));
    }

    [Fact]
    public async Task The_adapters_default_base_url_is_used_when_none_is_given()
    {
        var adapter = new ScriptedModelAdapter("scripted") { DefaultBaseUrl = new Uri("http://localhost:11434/v1") };

        var dto = await Service(adapter).CreateAsync(new SaveProviderRequest("Local", "scripted"));

        dto.BaseUrl.ShouldBe("http://localhost:11434/v1");
    }

    [Fact]
    public async Task Removing_a_provider_puts_orbs_that_pinned_it_back_on_auto()
    {
        var service = Service(new ScriptedModelAdapter("scripted"));
        var dto = await service.CreateAsync(new SaveProviderRequest("Lab", "scripted"));
        var orb = Orb.Create("Pip", DateTimeOffset.UnixEpoch);
        orb.UseModel(new ModelRef(dto.Id, "m"), DateTimeOffset.UnixEpoch);
        _store.OrbItems.Add(orb);

        await service.DeleteAsync(dto.Id);

        orb.Model.ShouldBeNull();
        _store.ProviderItems.Items.ShouldBeEmpty();
    }
}

public class ContextServiceTests
{
    private readonly InMemoryStore _store = new();

    private ContextService Service(params IContextConnector[] connectors)
    {
        var models = new PluginRegistry<IModelAdapter>([]);
        var providers = new ProviderService(_store.Providers, _store.Orbs, models, new FakeSecretProtector(), _store, FakeClock.Default, new CurrentUser());
        var indexer = new ContextIndexer(new EmbedderResolver(_store.Providers, models, providers), new JobScheduler(_store.Jobs, _store, FakeClock.Default), new CurrentUser());
        return new(_store.Context, _store.Orbs, new PluginRegistry<IContextConnector>(connectors), indexer, _store, FakeClock.Default);
    }

    [Fact]
    public async Task New_sources_are_read_straight_away()
    {
        var dto = await Service(new StubConnector("text", s => s.Input.ToUpperInvariant()))
            .CreateAsync(new SaveContextSourceRequest("Note", "text", Input: "hello"));

        dto.Status.ShouldBe(ContextSourceStatus.Ready);
        _store.ContextItems.Items.Single().Content.ShouldBe("HELLO");
    }

    [Fact]
    public async Task A_failed_read_is_recorded_not_thrown()
    {
        var dto = await Service(new StubConnector("url", _ => throw new HttpRequestException("404"), usesLocation: true, usesInput: false))
            .CreateAsync(new SaveContextSourceRequest("Docs", "url", Location: "https://example.com"));

        dto.Status.ShouldBe(ContextSourceStatus.Failed);
        dto.LastError.ShouldBe("404");
    }

    [Fact]
    public async Task Missing_input_is_refused()
    {
        await Should.ThrowAsync<DomainException>(() =>
            Service(new StubConnector("text", s => s.Input)).CreateAsync(new SaveContextSourceRequest("Note", "text")));
    }

    [Fact]
    public async Task Deleting_a_source_unlinks_it_from_every_orb()
    {
        var service = Service(new StubConnector("text", s => s.Input));
        var dto = await service.CreateAsync(new SaveContextSourceRequest("Note", "text", Input: "x"));
        var orb = Orb.Create("Pip", DateTimeOffset.UnixEpoch);
        orb.LinkContext(dto.Id, DateTimeOffset.UnixEpoch);
        _store.OrbItems.Add(orb);

        await service.DeleteAsync(dto.Id);

        orb.ContextSourceIds.ShouldBeEmpty();
    }
}
