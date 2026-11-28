using Orbs.Domain.Common;

namespace Orbs.Domain.Users;

/// <summary>
/// Someone who signs in to Orbs. Every orb, provider, context source and chat belongs to one user.
/// A user signs in with a password, a passkey or an external OpenID Connect account.
/// </summary>
public sealed class User : AggregateRoot
{
    public const int MaxEmailLength = 254;
    public const int MaxNameLength = 80;
    public const int MinPasswordLength = 10;

    private readonly List<Passkey> _passkeys = [];
    private readonly List<ExternalLogin> _externalLogins = [];

    private User(Guid id, string email, string displayName, bool isAdmin, DateTimeOffset now) : base(id, now)
    {
        Email = email;
        DisplayName = displayName;
        IsAdmin = isAdmin;
    }

    // For EF Core materialization.
    private User()
    {
        Email = string.Empty;
        DisplayName = string.Empty;
    }

    /// <summary>Lower-case and unique. Used to sign in with a password.</summary>
    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>Admins manage other accounts.</summary>
    public bool IsAdmin { get; private set; }

    /// <summary>A salted hash, or <c>null</c> for accounts that only use passkeys or an external login.</summary>
    public string? PasswordHash { get; private set; }

    public IReadOnlyList<Passkey> Passkeys => _passkeys;

    public IReadOnlyList<ExternalLogin> ExternalLogins => _externalLogins;

    public bool HasPassword => PasswordHash is not null;

    /// <summary>When notifications wait instead of arriving, or null for never.</summary>
    public QuietHours? QuietHours { get; private set; }

    public void SetQuietHours(QuietHours? quietHours, DateTimeOffset now)
    {
        QuietHours = quietHours;
        Touch(now);
    }

    public static User Create(string email, string? displayName, bool isAdmin, DateTimeOffset now, Guid? id = null)
    {
        var normalized = NormalizeEmail(email);
        var name = string.IsNullOrWhiteSpace(displayName) ? normalized.Split('@')[0] : displayName;
        return new User(id ?? Guid.NewGuid(), normalized, Guard.Text(name, nameof(DisplayName), MaxNameLength), isAdmin, now);
    }

    public static string NormalizeEmail(string? email)
    {
        var value = Guard.Text(email, "Email", MaxEmailLength).ToLowerInvariant();
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at == value.Length - 1 || value.Contains(' ', StringComparison.Ordinal))
        {
            throw new DomainException("Email must look like name@example.com.");
        }

        return value;
    }

    /// <summary>Checks the length rule before a password is hashed.</summary>
    public static void EnsureStrongEnough(string? password)
    {
        if (password is null || password.Length < MinPasswordLength)
        {
            throw new DomainException($"A password needs at least {MinPasswordLength} characters.");
        }
    }

    public void Rename(string displayName, DateTimeOffset now)
    {
        DisplayName = Guard.Text(displayName, nameof(DisplayName), MaxNameLength);
        Touch(now);
    }

    public void SetPasswordHash(string? passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        Touch(now);
    }

    public Passkey AddPasskey(byte[] credentialId, byte[] publicKey, uint signCount, string? name, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(publicKey);
        if (FindPasskey(credentialId) is not null)
        {
            throw new DomainException("This passkey is already registered.");
        }

        var label = string.IsNullOrWhiteSpace(name) ? $"Passkey {_passkeys.Count + 1}" : name;
        var passkey = new Passkey(Guid.NewGuid(), credentialId, publicKey, signCount, Guard.Text(label, "Name", Passkey.MaxNameLength), now);
        _passkeys.Add(passkey);
        Touch(now);
        return passkey;
    }

    public void RemovePasskey(Guid passkeyId, DateTimeOffset now)
    {
        var passkey = _passkeys.FirstOrDefault(p => p.Id == passkeyId) ?? throw new DomainException("That passkey does not exist.");
        if (!HasPassword && _externalLogins.Count == 0 && _passkeys.Count == 1)
        {
            throw new DomainException("This is your only way to sign in. Set a password or add another passkey first.");
        }

        _passkeys.Remove(passkey);
        Touch(now);
    }

    public Passkey? FindPasskey(ReadOnlySpan<byte> credentialId)
    {
        foreach (var passkey in _passkeys)
        {
            if (passkey.CredentialId.AsSpan().SequenceEqual(credentialId))
            {
                return passkey;
            }
        }

        return null;
    }

    public void LinkExternalLogin(string issuer, string subject, DateTimeOffset now)
    {
        if (_externalLogins.Any(l => l.Issuer == issuer && l.Subject == subject))
        {
            return;
        }

        _externalLogins.Add(new ExternalLogin(
            Guid.NewGuid(),
            Guard.Text(issuer, nameof(issuer), ExternalLogin.MaxIssuerLength),
            Guard.Text(subject, nameof(subject), ExternalLogin.MaxSubjectLength),
            now));
        Touch(now);
    }
}

/// <summary>A WebAuthn credential: a public key whose private half stays on the user's device.</summary>
public sealed class Passkey : Entity
{
    public const int MaxNameLength = 80;

    internal Passkey(Guid id, byte[] credentialId, byte[] publicKey, uint signCount, string name, DateTimeOffset createdAt) : base(id)
    {
        CredentialId = credentialId;
        PublicKey = publicKey;
        SignCount = signCount;
        Name = name;
        CreatedAt = createdAt;
    }

    // For EF Core materialization.
    private Passkey()
    {
        CredentialId = [];
        PublicKey = [];
        Name = string.Empty;
    }

#pragma warning disable CA1819 // Raw credential bytes, stored as-is.
    public byte[] CredentialId { get; private set; }

    public byte[] PublicKey { get; private set; }
#pragma warning restore CA1819

    public uint SignCount { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public void MarkUsed(uint signCount, DateTimeOffset now)
    {
        SignCount = signCount;
        LastUsedAt = now;
    }
}

/// <summary>An account at an OpenID Connect provider (issuer plus subject) that signs in as this user.</summary>
public sealed class ExternalLogin : Entity
{
    public const int MaxIssuerLength = 500;
    public const int MaxSubjectLength = 255;

    internal ExternalLogin(Guid id, string issuer, string subject, DateTimeOffset createdAt) : base(id)
    {
        Issuer = issuer;
        Subject = subject;
        CreatedAt = createdAt;
    }

    // For EF Core materialization.
    private ExternalLogin()
    {
        Issuer = string.Empty;
        Subject = string.Empty;
    }

    public string Issuer { get; private set; }

    public string Subject { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
