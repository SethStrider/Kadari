namespace Kadari.Application.Abstractions.Reports;

/// <summary>
/// Mints and validates short-lived signed links to a Decision Report's PDF (PAS-002 §9).
/// </summary>
/// <remarks>
/// <para>
/// The PDF endpoint is unauthenticated by necessity — the WhatsApp provider fetches the media itself
/// and carries no KADARI credentials. It was previously keyed on the report id alone, which made
/// every issued report a permanent, world-readable URL for a document full of personal information:
/// no expiry, no revocation, and no check that the report had even been approved.
/// </para>
/// <para>
/// A signed link keeps the endpoint anonymous while making the grant narrow and temporary.
/// </para>
/// </remarks>
public interface IReportMediaLinkFactory
{
    /// <summary>The path + query a provider should fetch, relative to the public base URL.</summary>
    string CreateLink(Guid reportId);

    /// <summary>Whether a presented signature is authentic and still within its window.</summary>
    bool IsValid(Guid reportId, long expiresAtUnixSeconds, string? signature);
}
