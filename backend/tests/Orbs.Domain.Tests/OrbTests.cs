using Orbs.Domain.Common;
using Orbs.Domain.Orbs;

namespace Orbs.Domain.Tests;

public class OrbTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_the_name_and_starts_with_the_default_look()
    {
        var orb = Orb.Create("  Pip  ", Now);

        orb.Name.ShouldBe("Pip");
        orb.Appearance.ShouldBe(OrbAppearance.Default);
        orb.Model.ShouldBeNull();
        orb.ContextSourceIds.ShouldBeEmpty();
        orb.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_a_missing_name(string? name)
    {
        Should.Throw<DomainException>(() => Orb.Create(name!, Now)).Message.ShouldContain("required");
    }

    [Fact]
    public void Create_rejects_a_name_that_is_too_long()
    {
        Should.Throw<DomainException>(() => Orb.Create(new string('x', Orb.MaxNameLength + 1), Now));
    }

    [Fact]
    public void Linking_the_same_source_twice_keeps_one_link()
    {
        var orb = Orb.Create("Pip", Now);
        var source = Guid.NewGuid();

        orb.LinkContext(source, Now);
        orb.LinkContext(source, Now);

        orb.ContextSourceIds.ShouldBe([source]);
    }

    [Fact]
    public void SetContext_replaces_links_and_drops_duplicates()
    {
        var orb = Orb.Create("Pip", Now);
        orb.LinkContext(Guid.NewGuid(), Now);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        orb.SetContext([a, b, a], Now);

        orb.ContextSourceIds.ShouldBe([a, b], ignoreOrder: true);
    }

    [Fact]
    public void SetContext_rejects_empty_ids()
    {
        var orb = Orb.Create("Pip", Now);

        Should.Throw<DomainException>(() => orb.SetContext([Guid.Empty], Now));
    }

    [Fact]
    public void UseModel_with_null_goes_back_to_auto()
    {
        var orb = Orb.Create("Pip", Now);
        orb.UseModel(new ModelRef(Guid.NewGuid(), "some-model"), Now);

        orb.UseModel(null, Now);

        orb.Model.ShouldBeNull();
    }

    [Fact]
    public void Changes_move_the_updated_timestamp()
    {
        var orb = Orb.Create("Pip", Now);
        var later = Now.AddMinutes(5);

        orb.Brief("Be brief.", later);

        orb.UpdatedAt.ShouldBe(later);
        orb.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Appearances_with_the_same_parts_are_equal()
    {
        var a = OrbAppearance.Default with { Color = OrbColor.Mint };
        var b = OrbAppearance.Default with { Color = OrbColor.Mint };

        a.ShouldBe(b);
    }
}
