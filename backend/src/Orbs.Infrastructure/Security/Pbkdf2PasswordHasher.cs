using System.Globalization;
using System.Security.Cryptography;
using Orbs.Application.Accounts;

namespace Orbs.Infrastructure.Security;

/// <summary>
/// PBKDF2 with SHA-512 and a random salt per password, stored as <c>pbkdf2-sha512.{iterations}.{salt}.{hash}</c>.
/// The iteration count is part of the stored value, so it can be raised later without breaking old hashes.
/// </summary>
internal sealed class Pbkdf2PasswordHasher(int iterations = Pbkdf2PasswordHasher.DefaultIterations) : IPasswordHasher
{
    /// <summary>The OWASP recommendation for PBKDF2-HMAC-SHA512.</summary>
    public const int DefaultIterations = 210_000;

    private const string Prefix = "pbkdf2-sha512";
    private const int SaltSize = 16;
    private const int HashSize = 64;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, HashSize);
        return string.Join('.', Prefix, iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hash)
    {
        ArgumentNullException.ThrowIfNull(password);
        var parts = (hash ?? string.Empty).Split('.');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var rounds))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA512, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
