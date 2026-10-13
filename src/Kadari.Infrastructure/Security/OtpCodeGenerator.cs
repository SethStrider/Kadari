using System.Security.Cryptography;
using System.Text;
using Kadari.Application.Abstractions.Identity;

namespace Kadari.Infrastructure.Security;

/// <summary>Cryptographically-random 6-digit codes, SHA-256 hashed for storage.</summary>
public sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    public string Generate() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
