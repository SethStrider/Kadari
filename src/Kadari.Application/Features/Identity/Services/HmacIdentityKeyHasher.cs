using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Identity;

namespace Kadari.Application.Features.Identity.Services;

/// <summary>
/// HMAC-SHA256 identity-key hasher (BCL only). The ID is normalised (digits/letters only, upper-cased) so
/// trivially-different renderings of the same number map to one key, then HMAC'd under the configured pepper.
/// </summary>
internal sealed class HmacIdentityKeyHasher(IOptions<IdentityHashOptions> options) : IIdentityKeyHasher
{
    public string Hash(string identityNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityNumber);

        var normalized = new string(identityNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var key = Encoding.UTF8.GetBytes(options.Value.Secret ?? string.Empty);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }
}
