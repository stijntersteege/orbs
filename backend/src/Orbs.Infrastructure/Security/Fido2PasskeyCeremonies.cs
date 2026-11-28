using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Orbs.Application.Accounts;
using Orbs.Domain.Common;
using Orbs.Domain.Users;

namespace Orbs.Infrastructure.Security;

/// <summary>Where passkeys are valid: the site's host name and the web origins allowed to use them.</summary>
public sealed record PasskeyOptions(string RelyingPartyId, string RelyingPartyName, IReadOnlySet<string> Origins);

/// <summary>
/// WebAuthn ceremonies with the Fido2 library. Challenges wait in memory for five minutes;
/// Orbs runs as one process, so there is no need to share them.
/// </summary>
internal sealed class Fido2PasskeyCeremonies(PasskeyOptions options, TimeProvider clock) : IPasskeyCeremonies
{
    private static readonly TimeSpan CeremonyLifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset Expires)> _pending = new();

    private readonly Fido2 _fido2 = new(new Fido2Configuration
    {
        RPID = options.RelyingPartyId,
        RPName = options.RelyingPartyName,
        Origins = new HashSet<string>(options.Origins, StringComparer.OrdinalIgnoreCase),
        TimestampDriftTolerance = 300_000,
    }, null);

    public PasskeyCeremony BeginRegistration(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var created = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = user.Id.ToByteArray(), Name = user.Email, DisplayName = user.DisplayName },
            ExcludeCredentials = [.. user.Passkeys.Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Preferred,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });
        return Remember(created.ToJson());
    }

    public async Task<NewPasskey> CompleteRegistrationAsync(string ceremonyId, string responseJson, CancellationToken cancellationToken = default)
    {
        var original = CredentialCreateOptions.FromJson(Take(ceremonyId));
        var response = Parse<AuthenticatorAttestationRawResponse>(responseJson);
        try
        {
            var result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = original,
                IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true),
            }, cancellationToken);
            return new NewPasskey(result.Id, result.PublicKey, result.SignCount);
        }
        catch (Fido2VerificationException ex)
        {
            throw new DomainException($"The passkey could not be verified: {ex.Message}");
        }
    }

    public PasskeyCeremony BeginSignIn()
    {
        var assertion = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Preferred,
        });
        return Remember(assertion.ToJson());
    }

    public byte[] ReadCredentialId(string responseJson) => Parse<AuthenticatorAssertionRawResponse>(responseJson).RawId;

    public async Task<uint> CompleteSignInAsync(string ceremonyId, string responseJson, Passkey stored, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var original = AssertionOptions.FromJson(Take(ceremonyId));
        var response = Parse<AuthenticatorAssertionRawResponse>(responseJson);
        try
        {
            var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = original,
                StoredPublicKey = stored.PublicKey,
                StoredSignatureCounter = stored.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (_, _) => Task.FromResult(true),
            }, cancellationToken);
            return result.SignCount;
        }
        catch (Fido2VerificationException ex)
        {
            throw new UnauthorizedException($"The passkey could not be verified: {ex.Message}");
        }
    }

    private PasskeyCeremony Remember(string optionsJson)
    {
        var now = clock.GetUtcNow();
        foreach (var (key, value) in _pending)
        {
            if (value.Expires < now)
            {
                _pending.TryRemove(key, out _);
            }
        }

        var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        _pending[id] = (optionsJson, now + CeremonyLifetime);
        return new PasskeyCeremony(id, optionsJson);
    }

    private string Take(string ceremonyId)
    {
        if (string.IsNullOrEmpty(ceremonyId) || !_pending.TryRemove(ceremonyId, out var entry) || entry.Expires < clock.GetUtcNow())
        {
            throw new DomainException("This passkey request expired. Try again.");
        }

        return entry.Json;
    }

    private static T Parse<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? throw new DomainException("The passkey answer was empty.");
        }
        catch (JsonException ex)
        {
            throw new DomainException($"The passkey answer could not be read: {ex.Message}");
        }
    }
}
