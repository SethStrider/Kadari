namespace Kadari.Infrastructure.Security;

/// <summary>
/// Signing configuration for Decision Report tamper-evidence (PS-004). A dedicated key — deliberately not
/// <c>Jwt:SigningKey</c> nor <c>ReportMedia:SigningKey</c>: this one never leaves the server (Trustli both
/// signs and verifies), so it has a narrower blast radius, and separate keys stop a leak in one place from
/// forging another.
/// </summary>
public sealed class ReportIntegrityOptions
{
    public const string SectionName = "ReportIntegrity";

    /// <summary>HMAC-SHA256 key, >= 32 bytes.</summary>
    public string SigningKey { get; init; } = string.Empty;
}
