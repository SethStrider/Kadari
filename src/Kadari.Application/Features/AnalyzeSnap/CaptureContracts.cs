using Kadari.Domain.Assets;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>
/// One object the on-device MediaPipe detector found in the still (ported from PoziSnap). The box is
/// normalised to 0..1 against the uploaded image, so it survives whatever resolution the client ran
/// inference at and whatever size it uploaded.
/// </summary>
public sealed record DetectedObject(
    string Label,
    double Confidence,
    double X,
    double Y,
    double Width,
    double Height);

/// <summary>
/// One item in a snap after the Kadari enrichment pass — an identified household asset, not a
/// PoziSnap listing. Carries identification (make/model via <see cref="DisplayName"/>, category,
/// condition, serial hint) and a <b>replacement</b> value (deck §17 — not resale), plus the normalised
/// box. The device COCO model has no class for a lamp or a rug, so the enricher may return items the
/// device never saw; <see cref="SourceIndex"/> ties an item back to the detection it came from (by
/// position in the request) or is null when the enricher found it itself.
/// </summary>
public sealed record IdentifiedItem(
    string Label,
    double Confidence,
    double X,
    double Y,
    double Width,
    double Height,
    string DisplayName,
    AssetCategory Category,
    string? Condition,
    string? SerialHint,
    Money? ReplacementValue,
    int? SourceIndex)
{
    /// <summary>A pass-through item from a raw detection, before any model enrichment.</summary>
    public static IdentifiedItem FromDetection(DetectedObject detection, int sourceIndex) => new(
        detection.Label,
        detection.Confidence,
        detection.X,
        detection.Y,
        detection.Width,
        detection.Height,
        DisplayName: detection.Label,
        Category: AssetCategory.Other,
        Condition: null,
        SerialHint: null,
        ReplacementValue: null,
        SourceIndex: sourceIndex);
}
