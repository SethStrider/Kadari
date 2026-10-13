namespace Kadari.WebApi.Configuration;

/// <summary>
/// Which upstream hops may be believed when they set <c>X-Forwarded-*</c> (PAS-002 §13.3).
/// </summary>
/// <remarks>
/// This is a prerequisite for rate limiting, not merely hygiene: IP-partitioned limits are trivially
/// evaded while any caller can spoof <c>X-Forwarded-For</c>. Empty lists mean "trust any hop", which
/// is the tunnel/demo posture — acceptable in Development, refused elsewhere by the readiness guard.
/// </remarks>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>Individual proxy addresses to trust (e.g. the ingress controller's pod IP).</summary>
    public string[] KnownProxies { get; init; } = [];

    /// <summary>CIDR networks to trust, as "prefix/length" (e.g. "10.0.0.0/8").</summary>
    public string[] KnownNetworks { get; init; } = [];

    /// <summary>
    /// How many proxy hops may rewrite the headers. Must match the real chain: too high lets a client
    /// inject a forged hop, too low leaves the original client address unresolved.
    /// </summary>
    public int ForwardLimit { get; init; } = 1;

    public bool TrustsAnyHop => KnownProxies.Length == 0 && KnownNetworks.Length == 0;
}
