namespace Kadari.Infrastructure.Security;

/// <summary>
/// Signing configuration for Decision Report media links (PAS-002 §9).
/// </summary>
/// <remarks>
/// A dedicated key, deliberately <b>not</b> <c>Jwt:SigningKey</c>. These links are handed to a third
/// party (the WhatsApp provider fetches the PDF itself), so the key has a wider blast radius than the
/// one minting access tokens; sharing it would let a leak in either place forge the other.
/// </remarks>
public sealed class ReportMediaOptions
{
    public const string SectionName = "ReportMedia";

    /// <summary>HMAC-SHA256 key, >= 32 bytes.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// How long a minted link stays valid. The default comfortably exceeds a provider's media fetch
    /// while keeping the window for a leaked URL short.
    /// </summary>
    public int LinkLifetimeMinutes { get; init; } = 15;
}
