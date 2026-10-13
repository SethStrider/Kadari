namespace Kadari.Application.Abstractions.Identity;

/// <summary>Generates opaque refresh tokens and hashes them for storage (never stored in the clear).</summary>
public interface IRefreshTokenService
{
    string Generate();

    string Hash(string token);
}
