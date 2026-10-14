using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Reports;

namespace Kadari.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 tamper-evidence signer for Decision Reports (PS-004). The signature is computed over the
/// report's canonical content with a server-held key, so only KADARI can produce a valid signature (proves
/// it was issued by KADARI) and any change to the content fails the constant-time check (proves it is
/// unaltered).
/// </summary>
public sealed class HmacReportIntegritySigner(IOptions<ReportIntegrityOptions> options) : IReportIntegritySigner
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.SigningKey);

    public string Sign(string canonicalContent)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalContent)));
    }

    public bool Verify(string canonicalContent, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(canonicalContent));
        var presented = Encoding.UTF8.GetBytes(signature);

        // Length-equality first: FixedTimeEquals requires equal lengths, and a signature's length is not secret.
        return expected.Length == presented.Length
               && CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}
