using Orbs.Domain.Common;
using Orbs.Domain.Users;

namespace Orbs.Domain.Tests;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Emails_are_trimmed_and_lower_cased_and_the_name_defaults_to_the_local_part()
    {
        var user = User.Create("  Sam@Example.COM ", null, isAdmin: false, Now);

        user.Email.ShouldBe("sam@example.com");
        user.DisplayName.ShouldBe("sam");
    }

    [Theory]
    [InlineData("sam")]
    [InlineData("@example.com")]
    [InlineData("sam@")]
    [InlineData("sam @example.com")]
    public void Malformed_emails_are_refused(string email)
    {
        Should.Throw<DomainException>(() => User.Create(email, null, false, Now));
    }

    [Fact]
    public void Short_passwords_are_refused()
    {
        Should.Throw<DomainException>(() => User.EnsureStrongEnough("short")).Message.ShouldContain("10");
        Should.NotThrow(() => User.EnsureStrongEnough("long enough!"));
    }

    [Fact]
    public void The_same_passkey_cannot_be_added_twice()
    {
        var user = User.Create("sam@example.com", null, false, Now);
        user.AddPasskey([1, 2, 3], [9], 0, null, Now).Name.ShouldBe("Passkey 1");

        Should.Throw<DomainException>(() => user.AddPasskey([1, 2, 3], [9], 0, "Again", Now));
        user.FindPasskey([1, 2, 3]).ShouldNotBeNull();
    }

    [Fact]
    public void The_last_way_to_sign_in_cannot_be_removed()
    {
        var user = User.Create("sam@example.com", null, false, Now);
        var only = user.AddPasskey([1], [9], 0, null, Now);

        Should.Throw<DomainException>(() => user.RemovePasskey(only.Id, Now)).Message.ShouldContain("only way");

        user.SetPasswordHash("hash", Now);
        user.RemovePasskey(only.Id, Now);
        user.Passkeys.ShouldBeEmpty();
    }

    [Fact]
    public void Sessions_slide_forward_when_used_but_not_on_every_request()
    {
        var session = Session.Start(Guid.NewGuid(), "hash", "Firefox", Now);
        session.ExpiresAt.ShouldBe(Now + Session.Lifetime);

        session.Seen(Now.AddMinutes(1)).ShouldBeFalse();
        session.Seen(Now.AddDays(1)).ShouldBeTrue();
        session.ExpiresAt.ShouldBe(Now.AddDays(1) + Session.Lifetime);
        session.IsExpired(Now.AddDays(40)).ShouldBeTrue();
    }

    [Fact]
    public void Owned_aggregates_get_their_owner_once_stored()
    {
        var orb = Orbs.Orb.Create("Pip", Now);
        orb.OwnerId.ShouldBe(Guid.Empty);

        var owner = Guid.NewGuid();
        orb.AssignOwner(owner);
        orb.OwnerId.ShouldBe(owner);
    }
}
