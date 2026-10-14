using System.Text;
using System.Text.Json;
using Kadari.Application.Abstractions.Reports;

namespace Kadari.Application.Features.Exit;

/// <summary>
/// The attributes of an asset offered for sale (spec §7). Carries a <see cref="PassportVerificationId"/> so a
/// marketplace (PoziSnap) can independently verify the "KADARI-verified provenance" that a plain marketplace
/// cannot offer — the whole point of the exit seam.
/// </summary>
public sealed record AssetForSalePackage(
    Guid AssetId,
    string PassportVerificationId,
    string DisplayName,
    string Category,
    string Assurance,
    long? ReplacementValueMinor,
    string? Currency,
    string? ImageReference,
    DateTimeOffset IssuedAt);

/// <summary>
/// The one-way, signed handoff format for the exit-to-PoziSnap seam (spec §7): a self-contained token
/// <c>base64url(payload) "." signature</c> that PoziSnap imports to pre-fill a listing and can verify without a
/// shared database or bus. The signature is an HMAC over the encoded payload with KADARI's server key
/// (<see cref="IReportIntegritySigner"/>), so only KADARI can mint one and any tampering fails the check.
/// </summary>
public static class AssetExportProtocol
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Encode(AssetForSalePackage package, IReportIntegritySigner signer)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(signer);

        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(package, Json));
        return $"{payload}.{signer.Sign(payload)}";
    }

    /// <summary>Verifies a token's signature and decodes its payload. Returns false (payload null) when the
    /// token is malformed or its signature does not check out.</summary>
    public static bool TryDecode(string? token, IReportIntegritySigner signer, out AssetForSalePackage? package)
    {
        ArgumentNullException.ThrowIfNull(signer);
        package = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var dot = token.LastIndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
        {
            return false;
        }

        var payload = token[..dot];
        var signature = token[(dot + 1)..];

        if (!signer.Verify(payload, signature))
        {
            return false;
        }

        try
        {
            package = JsonSerializer.Deserialize<AssetForSalePackage>(Base64UrlDecode(payload), Json);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return false;
        }

        return package is not null;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }
}
