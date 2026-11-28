using Orbs.Domain.Users;

namespace Orbs.Application.Accounts;

/// <summary>What the sign-in screen needs to know before anyone is signed in.</summary>
public sealed record AuthStatusDto(bool SetupRequired, bool PasswordSignIn, bool Passkeys, OidcStatusDto? Oidc);

public sealed record OidcStatusDto(string DisplayName);

public sealed record SetupRequest(string Email, string? DisplayName, string Password);

public sealed record SignInRequest(string Email, string Password);

/// <summary>A new session. Browsers get the token as a cookie as well; other clients send it as a bearer token.</summary>
public sealed record SignInResult(string Token, DateTimeOffset ExpiresAt, UserDto User);

public sealed record UserDto(Guid Id, string Email, string DisplayName, bool IsAdmin, bool HasPassword, DateTimeOffset CreatedAt);

public sealed record AccountDto(UserDto User, IReadOnlyList<PasskeyDto> Passkeys, IReadOnlyList<SessionDto> Sessions, bool HasExternalLogin);

public sealed record PasskeyDto(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

public sealed record SessionDto(Guid Id, string Description, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, bool Current);

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record ChangePasswordRequest(string? CurrentPassword, string NewPassword);

public sealed record CreateUserRequest(string Email, string? DisplayName, string Password, bool IsAdmin = false);

/// <summary>An admin changes someone's account. A new password signs that person out everywhere.</summary>
public sealed record UpdateUserRequest(string DisplayName, bool IsAdmin, string? NewPassword = null);

public sealed record PasskeyOptionsDto(string CeremonyId, string Options);

public sealed record CompletePasskeyRequest(string CeremonyId, string Response, string? Name = null);

/// <summary>A verified identity from an OpenID Connect provider.</summary>
public sealed record ExternalIdentity(string Issuer, string Subject, string? Email, bool EmailVerified, string? DisplayName);

internal static class AccountMapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.DisplayName, u.IsAdmin, u.HasPassword, u.CreatedAt);

    public static PasskeyDto ToDto(this Passkey p) => new(p.Id, p.Name, p.CreatedAt, p.LastUsedAt);
}
