namespace Kadari.WebApi.Configuration;

/// <summary>
/// Browser origins allowed to call the API (PAS-002 §4). The SPA sends credentials, so a wildcard
/// origin is not an option — every origin is named.
/// </summary>
public sealed class ClientCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Absolute origins, scheme included and no trailing slash (e.g. "https://app.trustli.co.za").</summary>
    public string[] AllowedOrigins { get; init; } = [];
}
