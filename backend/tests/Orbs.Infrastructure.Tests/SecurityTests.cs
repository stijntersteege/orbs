using Orbs.Domain.Common;
using Orbs.Domain.Users;
using Orbs.Infrastructure.Security;

namespace Orbs.Infrastructure.Tests;

public class SecurityTests
{
    private static readonly PasskeyOptions Site = new("orbs.example", "Orbs", new HashSet<string> { "https://orbs.example" });

    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void A_password_verifies_against_its_own_hash_only()
    {
        var hash = _hasher.Hash("correct horse battery");

        hash.ShouldStartWith("pbkdf2-sha512.1000.");
        _hasher.Verify("correct horse battery", hash).ShouldBeTrue();
        _hasher.Verify("Correct horse battery", hash).ShouldBeFalse();
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time()
    {
        _hasher.Hash("same password").ShouldNotBe(_hasher.Hash("same password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("pbkdf2-sha512.x.y.z")]
    [InlineData("bcrypt.1.2.3")]
    public void Garbage_hashes_never_verify(string hash)
    {
        _hasher.Verify("anything", hash).ShouldBeFalse();
    }

    [Fact]
    public void Passkey_registration_options_name_the_site_and_the_user()
    {
        var ceremonies = new Fido2PasskeyCeremonies(Site, TimeProvider.System);
        var user = User.Create("sam@example.com", "Sam", false, DateTimeOffset.UnixEpoch);

        var ceremony = ceremonies.BeginRegistration(user);

        ceremony.CeremonyId.ShouldNotBeNullOrEmpty();
        ceremony.OptionsJson.ShouldContain("orbs.example");
        ceremony.OptionsJson.ShouldContain("sam@example.com");
        ceremony.OptionsJson.ShouldContain("challenge");
    }

    [Fact]
    public async Task An_unknown_passkey_ceremony_is_refused()
    {
        var ceremonies = new Fido2PasskeyCeremonies(Site, TimeProvider.System);

        (await Should.ThrowAsync<DomainException>(() => ceremonies.CompleteRegistrationAsync("nope", "{}"))).Message.ShouldContain("expired");
    }
}
