using Orbs.Application.Abstractions;
using Orbs.Domain.Users;

namespace Orbs.Application.Accounts;

public interface IUserRepository : IRepository<User>
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<User?> FindByExternalLoginAsync(string issuer, string subject, CancellationToken cancellationToken = default);

    Task<User?> FindByPasskeyAsync(byte[] credentialId, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}

public interface ISessionRepository : IRepository<Session>
{
    Task<Session?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Session>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes sessions that expired before <paramref name="now"/>. Returns how many.</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>Hashes and checks passwords with a slow, salted algorithm.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>Housekeeping across every user's data.</summary>
public interface IOwnershipMaintenance
{
    /// <summary>Gives data from before accounts existed (no owner) to this user.</summary>
    Task ClaimUnownedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes everything a user owns.</summary>
    Task DeleteOwnedAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>A passkey the browser just created, checked and ready to store.</summary>
public sealed record NewPasskey(byte[] CredentialId, byte[] PublicKey, uint SignCount);

/// <summary>
/// Runs WebAuthn ceremonies. Options go to the browser as JSON; its answers come back as JSON.
/// Each ceremony has an id that ties the answer to the challenge the server sent.
/// </summary>
public interface IPasskeyCeremonies
{
    PasskeyCeremony BeginRegistration(User user);

    Task<NewPasskey> CompleteRegistrationAsync(string ceremonyId, string responseJson, CancellationToken cancellationToken = default);

    PasskeyCeremony BeginSignIn();

    /// <summary>The credential id inside a sign-in answer, to look up who it belongs to.</summary>
    byte[] ReadCredentialId(string responseJson);

    /// <summary>Checks the signature against the stored key. Returns the new signature counter.</summary>
    Task<uint> CompleteSignInAsync(string ceremonyId, string responseJson, Passkey stored, CancellationToken cancellationToken = default);
}

public sealed record PasskeyCeremony(string CeremonyId, string OptionsJson);
