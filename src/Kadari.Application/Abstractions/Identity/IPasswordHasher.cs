namespace Kadari.Application.Abstractions.Identity;

/// <summary>
/// Hashes and verifies account passwords. The produced hash is opaque to the domain — a
/// <see cref="Kadari.Domain.Identity.UserAccount"/> stores it verbatim and never inspects it, exactly
/// as OTP codes are hashed outside the aggregate. Implementations must use a slow, salted KDF and a
/// constant-time comparison.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a self-describing hash (algorithm parameters + salt embedded) for storage.</summary>
    string Hash(string password);

    /// <summary>Constant-time verification of a candidate password against a stored hash.</summary>
    bool Verify(string hash, string password);
}
