namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>
/// The still and its on-device detections, handed to whatever enriches them. The image is a disk-backed
/// handle, not bytes — an implementation should read it once, as late as it can, and send the single
/// still (optionally with crops) in one request rather than calling per object.
/// </summary>
public sealed record VisionEnrichmentRequest(
    SnapImage Image,
    IReadOnlyList<DetectedObject> Detections,
    string? RoomLabel);

/// <summary>
/// The enricher's answer. <see cref="Enriched"/> is false when no provider is configured, so the UI can
/// say "awaiting enrichment" rather than implying a model produced empty fields.
/// </summary>
public sealed record VisionEnrichmentResult(bool Enriched, IReadOnlyList<IdentifiedItem> Items)
{
    /// <summary>What a model call used, when one was made — recorded by capture through the one usage writer.</summary>
    public Abstractions.Intelligence.ModelCallUsage? Usage { get; init; }
}

/// <summary>
/// Turns raw detections into identified household assets — make/model, category, condition and a
/// <b>replacement</b> value (deck §17). This is the Kadari counterpart of PoziSnap's
/// <c>IDetectionEnricher</c>: same seam and plumbing, but it identifies assets for insurance rather than
/// producing resale listings. The concrete implementation (Gemini) lives in Infrastructure (D7).
/// </summary>
public interface IVisionEnricher
{
    Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this enricher sends the home still to a third-party AI outside South Africa (POPIA cross-border
    /// disclosure, D8/spec §6). A cross-border enricher runs only when the customer has given the distinct
    /// cross-border-AI consent; without it the capture pipeline keeps the images on-shore and falls back to the
    /// on-device detections. Local / pass-through enrichers are not cross-border. Defaults to false.
    /// </summary>
    bool IsCrossBorder => false;

    /// <summary>
    /// Whether each call is billed (a hosted model). Metered calls count against the customer's daily vision
    /// quota (AS-09); the pass-through enricher is free and is not counted. Defaults to false.
    /// </summary>
    bool IsMetered => false;
}
