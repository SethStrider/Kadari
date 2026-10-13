using System.Security.Cryptography;
using System.Text;
using Kadari.Application.Abstractions.Identity;

namespace Kadari.Infrastructure.Security;

/// <summary>Opaque 256-bit refresh tokens (URL-safe base64), SHA-256 hashed for storage.</summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    public string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
