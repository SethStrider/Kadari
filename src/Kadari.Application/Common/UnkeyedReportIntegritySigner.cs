using System.Security.Cryptography;
using System.Text;
using Kadari.Application.Abstractions.Reports;

namespace Kadari.Application.Common;

/// <summary>
/// Keyless fallback <see cref="IReportIntegritySigner"/> for hosts without Infrastructure (tests, in-memory
/// composition). Self-consistent — a report it seals verifies — so tamper <em>detection</em> works, but it
/// is not forge-resistant (no secret). A composed app replaces it with the HMAC signer (server key) via
/// <c>AddInfrastructure</c>, which is always present in real hosts.
/// </summary>
internal sealed class UnkeyedReportIntegritySigner : IReportIntegritySigner
{
    public string Sign(string canonicalContent)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalContent)));

    public bool Verify(string canonicalContent, string signature)
    {
        var expected = Encoding.UTF8.GetBytes(Sign(canonicalContent));
        var presented = Encoding.UTF8.GetBytes(signature ?? string.Empty);
        return expected.Length == presented.Length && CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}
