using Kadari.Application.Features.AnalyzeSnap;

namespace Kadari.Application.Features.Valuation;

/// <summary>
/// Routes an identified item to the first registered <see cref="IValuationRail"/> that values its category
/// (deck §18). Registration order is precedence: a future professional rail registered ahead of the AI
/// baseline would claim jewellery/art, leaving the baseline for everything else. In v1 only the AI baseline
/// is registered, so it always wins — but the seam is what lets a rail be added without touching capture.
/// </summary>
internal sealed class ValuationRailRouter(IEnumerable<IValuationRail> rails)
{
    private readonly IReadOnlyList<IValuationRail> _rails = rails.ToList();

    public Valuation? Value(IdentifiedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        foreach (var rail in _rails)
        {
            if (rail.CanValue(item.Category))
            {
                return rail.Value(item);
            }
        }

        return null;
    }
}
