namespace Kadari.Application.Abstractions.Reports;

/// <summary>
/// Seals and verifies a Decision Report's tamper-evidence signature (PS-004). Implemented in Infrastructure
/// with an HMAC over the report's canonical content and a server-held key, so only KADARI can produce a
/// valid signature and any alteration of the content breaks it.
/// </summary>
public interface IReportIntegritySigner
{
    string Sign(string canonicalContent);

    bool Verify(string canonicalContent, string signature);
}
