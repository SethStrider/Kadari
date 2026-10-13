using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Features.Valuation;

/// <summary>
/// A replacement-value assertion produced by a valuation rail (deck §17–18). <see cref="Strength"/> decides
/// whether it lifts the asset up the assurance ladder: a <see cref="EvidenceStrength.SelfAsserted"/> AI
/// baseline is a displayed number that never reaches ValueSupported on its own (D10), whereas an
/// <see cref="EvidenceStrength.Independent"/> valuation (a professional rail, later) carries a
/// <see cref="Reference"/> to its supporting artefact and does lift assurance.
/// </summary>
public sealed record Valuation(Money Amount, EvidenceStrength Strength, string Source, string? Reference = null);

/// <summary>
/// A replacement-value intelligence rail for a family of assets (deck §18 — electronics resolve to an
/// equivalent SKU, jewellery/art need a professional valuation). Rails are ordered and category-routed by
/// <see cref="ValuationRailRouter"/>; the first that <see cref="CanValue"/> the asset's category wins. Kept a
/// pure Application seam (no I/O in v1) so the AI baseline is unit-testable; an external-lookup or
/// human-in-the-loop rail can implement it later without touching the capture pipeline.
/// </summary>
public interface IValuationRail
{
    /// <summary>Whether this rail values the given category.</summary>
    bool CanValue(AssetCategory category);

    /// <summary>Produce a replacement value for the identified item, or null when the rail cannot price it.</summary>
    Valuation? Value(IdentifiedItem item);
}
