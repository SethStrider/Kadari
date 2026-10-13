namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>
/// Daily caps on paid vision-model calls (AS-09). Past a cap, capture still works but falls back to the on-device
/// detections, exactly as without the cross-border consent: the customer keeps capturing and nothing is billed.
/// A cap of 0 means no cap.
/// </summary>
public sealed class VisionQuotaOptions
{
    public const string SectionName = "Kadari:Vision";

    /// <summary>Metered calls one customer may make per UTC day. A household capture is a few dozen stills.</summary>
    public int DailyCallsPerCustomer { get; set; } = 200;

    /// <summary>Metered calls across all customers per UTC day: the platform's spend ceiling. Off by default.</summary>
    public int DailyCallsTotal { get; set; }
}
