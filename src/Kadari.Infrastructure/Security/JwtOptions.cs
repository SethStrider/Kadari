namespace Kadari.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;   // >= 32 bytes for HS256
    public string Issuer { get; set; } = "trustli";
    public string Audience { get; set; } = "trustli";

    /// <summary>
    /// Access-token lifetime. Kept short (PAS-002 §10): the refresh token carries session length, so a
    /// long-lived access token buys nothing and only widens the window in which a revoked session's
    /// token still works. Session state is checked per request, but not every deployment path is
    /// cache-coherent — a short life is the backstop.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;
}
