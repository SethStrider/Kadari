namespace Kadari.Application.Abstractions.Identity;

/// <summary>
/// Turns a raw national ID into a stable, non-reversible identity key — a keyed hash (HMAC) so two records
/// for the same person produce the same key, yet the raw ID cannot be recovered or brute-forced from it
/// without the server-side pepper. This is what lets a passport claim match a claimant to a subject without
/// the subject ever storing the ID.
/// </summary>
public interface IIdentityKeyHasher
{
    string Hash(string identityNumber);
}
