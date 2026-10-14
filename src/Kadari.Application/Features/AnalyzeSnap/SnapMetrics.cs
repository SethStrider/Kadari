using System.Diagnostics.Metrics;

namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>
/// What happened to each capture (AS-27), so a rise in fallbacks shows on a dashboard instead of only in the logs.
/// Counter <c>kadari.snaps</c>, tagged <c>enrichment</c> (<c>enriched</c>, <c>no_cross_border_consent</c>,
/// <c>quota_reached</c>, <c>enricher_fallback</c>: the model was asked but timed out, failed or returned nothing usable)
/// and <c>items</c> (<c>found</c> or <c>none</c>). Recorded once per capture, not per retried transaction.
/// </summary>
internal static class SnapMetrics
{
    public const string MeterName = "Kadari.Capture";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Snaps = Meter.CreateCounter<long>(
        "kadari.snaps", unit: "{snap}", description: "KADARI captures analysed, by how they were enriched.");

    public static void Record(string enrichment, bool foundItems) =>
        Snaps.Add(1,
            new KeyValuePair<string, object?>("enrichment", enrichment),
            new KeyValuePair<string, object?>("items", foundItems ? "found" : "none"));
}
