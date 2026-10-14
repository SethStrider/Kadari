using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using AssetValuation = Kadari.Application.Features.Valuation.Valuation;

namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>
/// Turns one enriched <see cref="IdentifiedItem"/> into a registered <see cref="Asset"/> with its opening
/// evidence — the captured image (Observed), the identification, and, when a valuation rail priced it, its
/// replacement value. The value's strength decides whether it lifts assurance (D10): a self-asserted AI
/// baseline holds at Identified, an independent valuation reaches ValueSupported. Pure, so the mapping is
/// unit-testable without the enrichment/valuation/storage plumbing around it.
/// </summary>
public static class CapturedAssetFactory
{
    /// <summary>Source tag for the captured still (the device), distinct from the enricher.</summary>
    public const string CaptureSource = "capture";

    public static Asset Create(
        TenantId tenantId, OwnerId ownerId, string imageKey, string enricherSource,
        IdentifiedItem item, AssetValuation? valuation, DateTimeOffset now, string? roomLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(enricherSource);

        var asset = Asset.Register(tenantId, ownerId, item.DisplayName, item.Category, now, roomLabel);

        asset.Attach(AssetEvidence.Observed(CaptureSource, imageKey, now));
        asset.Attach(AssetEvidence.Identified(enricherSource, Math.Clamp(item.Confidence, 0, 1), now));

        if (valuation is not null)
        {
            asset.Attach(valuation.Strength == EvidenceStrength.Independent
                ? AssetEvidence.ValueSupported(valuation.Source, valuation.Amount, valuation.Reference!, now)
                : AssetEvidence.ValueEstimated(valuation.Source, valuation.Amount, now));
        }

        return asset;
    }
}
