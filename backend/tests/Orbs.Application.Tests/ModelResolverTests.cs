using Orbs.Application.Common;
using Orbs.Application.Conversations;
using Orbs.Domain.Orbs;
using Orbs.Domain.Providers;

namespace Orbs.Application.Tests;

public class ModelResolverTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static ModelProvider Provider(string name, string? defaultModel, DateTimeOffset createdAt, bool enabled = true)
    {
        var provider = ModelProvider.Create(name, "echo", createdAt);
        provider.SetDefaultModel(defaultModel, createdAt);
        if (!enabled)
        {
            provider.Disable(createdAt);
        }

        return provider;
    }

    [Fact]
    public void A_pinned_model_wins()
    {
        var auto = Provider("Auto", "auto-model", Now);
        var pinned = Provider("Pinned", null, Now.AddDays(1));
        var orb = Orb.Create("Pip", Now);
        orb.UseModel(new ModelRef(pinned.Id, "special"), Now);

        var resolved = ModelResolver.Resolve(orb, [auto, pinned]);

        resolved.Provider.ShouldBe(pinned);
        resolved.Model.ShouldBe("special");
    }

    [Fact]
    public void A_disabled_pinned_provider_falls_back_to_auto()
    {
        var auto = Provider("Auto", "auto-model", Now);
        var pinned = Provider("Pinned", null, Now, enabled: false);
        var orb = Orb.Create("Pip", Now);
        orb.UseModel(new ModelRef(pinned.Id, "special"), Now);

        ModelResolver.Resolve(orb, [auto, pinned]).Model.ShouldBe("auto-model");
    }

    [Fact]
    public void Auto_picks_the_oldest_enabled_provider_with_a_default_model()
    {
        var newer = Provider("Newer", "newer-model", Now.AddDays(2));
        var older = Provider("Older", "older-model", Now.AddDays(1));
        var noDefault = Provider("NoDefault", null, Now);

        ModelResolver.Resolve(Orb.Create("Pip", Now), [newer, noDefault, older]).Provider.ShouldBe(older);
    }

    [Fact]
    public void Without_any_usable_provider_the_orb_cannot_answer()
    {
        Should.Throw<ModelUnavailableException>(() =>
            ModelResolver.Resolve(Orb.Create("Pip", Now), [Provider("Off", "m", Now, enabled: false)]));
    }
}
