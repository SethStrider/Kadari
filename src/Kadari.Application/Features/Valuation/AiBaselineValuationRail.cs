using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Valuation;

/// <summary>
/// The AI baseline replacement-value rail (D10): it reads the vision enricher's estimate carried on the
/// identified item and surfaces it as a <b>self-asserted</b> valuation — a helpful displayed number that,
/// on its own, never reaches ValueSupported. A professional/lookup rail (deferred) is what upgrades a
/// category to an independent value. For v1 this rail covers <i>every</i> category (the routing stub), so
/// the router always resolves to it.
/// </summary>
internal sealed class AiBaselineValuationRail : IValuationRail
{
    /// <summary>Provenance tag recorded on the value evidence — an AI baseline, not a specific model.</summary>
    public const string RailSource = "ai-baseline";

    public bool CanValue(AssetCategory category) => true;

    public Valuation? Value(IdentifiedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.ReplacementValue is null
            ? null
            : new Valuation(item.ReplacementValue, EvidenceStrength.SelfAsserted, RailSource);
    }
}
