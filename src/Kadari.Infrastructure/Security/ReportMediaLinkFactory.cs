using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Reports;

namespace Kadari.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 signed, expiring links to report media (PAS-002 §9).
/// </summary>
/// <remarks>
/// The signature covers both the report id and the expiry, so neither can be edited independently —
/// signing the id alone would let a holder extend their own window.
/// </remarks>
public sealed class ReportMediaLinkFactory(IOptions<ReportMediaOptions> options, TimeProvider clock)
    : IReportMediaLinkFactory
{
    private readonly ReportMediaOptions _options = options.Value;

    public string CreateLink(Guid reportId)
    {
        var expires = clock.GetUtcNow().AddMinutes(_options.LinkLifetimeMinutes).ToUnixTimeSeconds();
        var signature = Sign(reportId, expires);

        return $"/api/v1/reports/{reportId}/pdf?expires={expires}&sig={Uri.EscapeDataString(signature)}";
    }

    public bool IsValid(Guid reportId, long expiresAtUnixSeconds, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        if (clock.GetUtcNow().ToUnixTimeSeconds() > expiresAtUnixSeconds)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(reportId, expiresAtUnixSeconds));
        var presented = Encoding.UTF8.GetBytes(signature);

        // Length-equality first: FixedTimeEquals requires equal lengths, and the length of a signature
        // is not a secret.
        return expected.Length == presented.Length
               && CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    private string Sign(Guid reportId, long expires)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        var payload = Encoding.UTF8.GetBytes($"{reportId:D}|{expires}");

        return Base64UrlEncode(hmac.ComputeHash(payload));
    }

    /// <summary>Base64url so the signature survives a query string without escaping.</summary>
    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
