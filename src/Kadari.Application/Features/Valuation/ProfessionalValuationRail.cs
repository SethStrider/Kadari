using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Valuation;

/// <summary>
/// The professional-valuation rail (deck §18, D10): for the categories a machine cannot credibly price —
/// jewellery and art — it <b>declines</b> to produce a capture-time value (returns null), so no misleading AI
/// estimate is attached and the asset waits for an independent valuer (see SubmitAssetValuation). Registered
/// <i>ahead</i> of the AI baseline, so the router's registration-order precedence hands it these categories
/// while everything else falls through to the baseline.
/// </summary>
internal sealed class ProfessionalValuationRail : IValuationRail
{
    public bool CanValue(AssetCategory category)
        => category is AssetCategory.Jewellery or AssetCategory.ArtAndCollectibles;

    // A professional valuation is human-in-the-loop; there is nothing to auto-fill at capture time.
    public Valuation? Value(IdentifiedItem item) => null;
}
