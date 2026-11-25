using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Application.Tests.Fakes;
using Orbs.Domain.Common;

namespace Orbs.Application.Tests;

public class PluginRegistryTests
{
    [Fact]
    public void Finds_plugins_by_kind_ignoring_case()
    {
        var adapter = new ScriptedModelAdapter("alpha");
        var registry = new PluginRegistry<IModelAdapter>([adapter]);

        registry.Get("ALPHA").ShouldBe(adapter);
        registry.Contains("alpha").ShouldBeTrue();
    }

    [Fact]
    public void Unknown_kinds_are_a_rule_violation_that_lists_the_known_ones()
    {
        var registry = new PluginRegistry<IModelAdapter>([new ScriptedModelAdapter("alpha")]);

        Should.Throw<DomainException>(() => registry.Get("beta")).Message.ShouldContain("alpha");
    }

    [Fact]
    public void Two_plugins_cannot_share_a_kind()
    {
        Should.Throw<InvalidOperationException>(() =>
            new PluginRegistry<IModelAdapter>([new ScriptedModelAdapter("alpha"), new ScriptedModelAdapter("alpha")]));
    }
}
