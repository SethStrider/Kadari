using System.Security.Cryptography;
using Kadari.Application.Abstractions.Identity;

namespace Kadari.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher (BCL only — no external dependency). The hash is self-describing:
/// <c>{iterations}.{salt}.{subkey}</c> with the two byte fields base64-encoded, so the work factor can be
/// raised over time without breaking existing hashes — <see cref="Verify"/> reads the iteration count
/// back out of the stored value rather than assuming today's default.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int SubkeyBytes = 32;
    private const int DefaultIterations = 210_000;   // OWASP 2023 guidance for PBKDF2-SHA256
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, Algorithm, SubkeyBytes);

        return $"{DefaultIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
    }

    public bool Verify(string hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var parts = hash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
