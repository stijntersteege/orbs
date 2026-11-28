using System.Security.Cryptography;
using System.Text;
using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Users;

namespace Orbs.Application.Accounts;

/// <summary>How people may sign in. Built from configuration by the host.</summary>
public sealed record AuthSettings(bool OidcEnabled = false, string OidcDisplayName = "Single sign-on", bool OidcCreatesAccounts = false);

/// <summary>Use cases for accounts: first-run setup, signing in and out, passkeys, sessions and user management.</summary>
public sealed class AccountService(
    IUserRepository users,
    ISessionRepository sessions,
    IPasswordHasher hasher,
    IPasskeyCeremonies passkeys,
    IOwnershipMaintenance ownership,
    IUnitOfWork unitOfWork,
    CurrentUser currentUser,
    AuthSettings settings,
    TimeProvider clock)
{
    private const string WrongCredentials = "That email and password do not match.";

    public async Task<AuthStatusDto> StatusAsync(CancellationToken cancellationToken = default) => new(
        SetupRequired: !await users.AnyAsync(cancellationToken),
        PasswordSignIn: true,
        Passkeys: true,
        Oidc: settings.OidcEnabled ? new OidcStatusDto(settings.OidcDisplayName) : null);

    /// <summary>Creates the first account, an admin who also receives any data from before accounts existed.</summary>
    public async Task<SignInResult> SetupAsync(SetupRequest request, string? device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await users.AnyAsync(cancellationToken))
        {
            throw new ConflictException("Orbs is already set up. Sign in instead.");
        }

        User.EnsureStrongEnough(request.Password);
        var now = clock.GetUtcNow();
        var user = User.Create(request.Email, request.DisplayName, isAdmin: true, now);
        user.SetPasswordHash(hasher.Hash(request.Password), now);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ownership.ClaimUnownedAsync(user.Id, cancellationToken);
        return await StartSessionAsync(user, device, cancellationToken);
    }

    public async Task<SignInResult> SignInWithPasswordAsync(SignInRequest request, string? device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string email;
        try
        {
            email = User.NormalizeEmail(request.Email);
        }
        catch (DomainException)
        {
            throw new UnauthorizedException(WrongCredentials);
        }

        var user = await users.FindByEmailAsync(email, cancellationToken);
        if (user?.PasswordHash is null || !hasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            throw new UnauthorizedException(WrongCredentials);
        }

        return await StartSessionAsync(user, device, cancellationToken);
    }

    public PasskeyOptionsDto BeginPasskeySignIn()
    {
        var ceremony = passkeys.BeginSignIn();
        return new PasskeyOptionsDto(ceremony.CeremonyId, ceremony.OptionsJson);
    }

    public async Task<SignInResult> CompletePasskeySignInAsync(CompletePasskeyRequest request, string? device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var credentialId = passkeys.ReadCredentialId(request.Response);
        var user = await users.FindByPasskeyAsync(credentialId, cancellationToken) ?? throw new UnauthorizedException("This passkey is not registered here.");
        var passkey = user.FindPasskey(credentialId)!;
        var counter = await passkeys.CompleteSignInAsync(request.CeremonyId, request.Response, passkey, cancellationToken);
        passkey.MarkUsed(counter, clock.GetUtcNow());
        return await StartSessionAsync(user, device, cancellationToken);
    }

    /// <summary>Signs in with an identity the OpenID Connect provider vouched for, linking or creating the account.</summary>
    public async Task<SignInResult> SignInWithExternalAsync(ExternalIdentity identity, string? device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var now = clock.GetUtcNow();
        var user = await users.FindByExternalLoginAsync(identity.Issuer, identity.Subject, cancellationToken);
        if (user is null && identity.EmailVerified && identity.Email is { Length: > 0 } email)
        {
            // Only a verified address may join an existing account; otherwise anyone could claim it.
            user = await users.FindByEmailAsync(User.NormalizeEmail(email), cancellationToken);
        }

        if (user is null)
        {
            var firstUser = !await users.AnyAsync(cancellationToken);
            if (!firstUser && !settings.OidcCreatesAccounts)
            {
                throw new ForbiddenException("There is no Orbs account for this login. Ask an admin to create one.");
            }

            if (identity.Email is not { Length: > 0 })
            {
                throw new ForbiddenException("The sign-in provider did not share an email address.");
            }

            user = User.Create(identity.Email, identity.DisplayName, isAdmin: firstUser, now);
            users.Add(user);
            if (firstUser)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await ownership.ClaimUnownedAsync(user.Id, cancellationToken);
            }
        }

        user.LinkExternalLogin(identity.Issuer, identity.Subject, now);
        return await StartSessionAsync(user, device, cancellationToken);
    }

    /// <summary>Finds the user behind a session token. Returns <c>null</c> for unknown or expired tokens.</summary>
    public async Task<User?> AuthenticateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await sessions.FindByTokenHashAsync(HashToken(token), cancellationToken);
        var now = clock.GetUtcNow();
        if (session is null || session.IsExpired(now))
        {
            return null;
        }

        var user = await users.FindAsync(session.UserId, cancellationToken);
        if (user is not null && session.Seen(now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    /// <summary>The account that the shared API token acts as: the first admin.</summary>
    public async Task<User?> FirstAdminAsync(CancellationToken cancellationToken = default) =>
        (await users.ListAsync(cancellationToken)).Where(u => u.IsAdmin).OrderBy(u => u.CreatedAt).FirstOrDefault();

    public async Task SignOutAsync(string token, CancellationToken cancellationToken = default)
    {
        var session = await sessions.FindByTokenHashAsync(HashToken(token), cancellationToken);
        if (session is not null)
        {
            sessions.Remove(session);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AccountDto> GetAccountAsync(string? currentToken, CancellationToken cancellationToken = default)
    {
        var user = await LoadCurrentAsync(cancellationToken);
        var currentHash = currentToken is null ? null : HashToken(currentToken);
        var list = await sessions.ListForUserAsync(user.Id, cancellationToken);
        var now = clock.GetUtcNow();
        return new AccountDto(
            user.ToDto(),
            [.. user.Passkeys.OrderBy(p => p.CreatedAt).Select(p => p.ToDto())],
            [.. list.Where(s => !s.IsExpired(now)).OrderByDescending(s => s.LastSeenAt)
                .Select(s => new SessionDto(s.Id, s.Description, s.CreatedAt, s.LastSeenAt, s.TokenHash == currentHash))],
            user.ExternalLogins.Count > 0);
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await LoadCurrentAsync(cancellationToken);
        user.Rename(request.DisplayName, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToDto();
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await LoadCurrentAsync(cancellationToken);
        if (user.PasswordHash is not null && !hasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            throw new DomainException("The current password is not right.");
        }

        User.EnsureStrongEnough(request.NewPassword);
        user.SetPasswordHash(hasher.Hash(request.NewPassword), clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var session = await sessions.FindAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            throw new NotFoundException("Session", sessionId);
        }

        sessions.Remove(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PasskeyOptionsDto> BeginPasskeyRegistrationAsync(CancellationToken cancellationToken = default)
    {
        var ceremony = passkeys.BeginRegistration(await LoadCurrentAsync(cancellationToken));
        return new PasskeyOptionsDto(ceremony.CeremonyId, ceremony.OptionsJson);
    }

    public async Task<PasskeyDto> CompletePasskeyRegistrationAsync(CompletePasskeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await LoadCurrentAsync(cancellationToken);
        var created = await passkeys.CompleteRegistrationAsync(request.CeremonyId, request.Response, cancellationToken);
        if (await users.FindByPasskeyAsync(created.CredentialId, cancellationToken) is { } owner && owner.Id != user.Id)
        {
            throw new ConflictException("This passkey belongs to another account.");
        }

        var passkey = user.AddPasskey(created.CredentialId, created.PublicKey, created.SignCount, request.Name, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return passkey.ToDto();
    }

    public async Task RemovePasskeyAsync(Guid passkeyId, CancellationToken cancellationToken = default)
    {
        var user = await LoadCurrentAsync(cancellationToken);
        user.RemovePasskey(passkeyId, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------- Admin ----------

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        currentUser.RequireAdmin();
        return [.. (await users.ListAsync(cancellationToken)).OrderBy(u => u.CreatedAt).Select(u => u.ToDto())];
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        currentUser.RequireAdmin();
        var email = User.NormalizeEmail(request.Email);
        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        User.EnsureStrongEnough(request.Password);
        var now = clock.GetUtcNow();
        var user = User.Create(email, request.DisplayName, request.IsAdmin, now);
        user.SetPasswordHash(hasher.Hash(request.Password), now);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToDto();
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        currentUser.RequireAdmin();
        if (userId == currentUser.UserId)
        {
            throw new DomainException("You cannot delete your own account.");
        }

        var user = await users.FindAsync(userId, cancellationToken) ?? throw new NotFoundException("User", userId);
        foreach (var session in await sessions.ListForUserAsync(userId, cancellationToken))
        {
            sessions.Remove(session);
        }

        users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ownership.DeleteOwnedAsync(userId, cancellationToken);
    }

    public static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<SignInResult> StartSessionAsync(User user, string? device, CancellationToken cancellationToken)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var session = Session.Start(user.Id, HashToken(token), device, clock.GetUtcNow());
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SignInResult(token, session.ExpiresAt, user.ToDto());
    }

    private async Task<User> LoadCurrentAsync(CancellationToken cancellationToken) =>
        await users.FindAsync(currentUser.RequireUserId(), cancellationToken) ?? throw new UnauthorizedException();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
