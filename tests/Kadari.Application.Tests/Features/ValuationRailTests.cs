using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.Valuation;
using Kadari.Domain.Assets;
using Kadari.Domain.ValueObjects;

namespace Kadari.Application.Tests.Features;

public class ValuationRailTests
{
    private static IdentifiedItem Item(Money? value, AssetCategory category = AssetCategory.Electronics) => new(
        Label: "tv", Confidence: 0.9, X: 0, Y: 0, Width: 1, Height: 1,
        DisplayName: "Samsung 65\" TV", Category: category,
        Condition: null, SerialHint: null, ReplacementValue: value, SourceIndex: 0);

    [Fact]
    public void AiBaseline_surfaces_the_enricher_estimate_as_a_self_asserted_value()
    {
        var rail = new AiBaselineValuationRail();

        var valuation = rail.Value(Item(Money.Zar(2_749_900)));

        Assert.NotNull(valuation);
        Assert.Equal(Money.Zar(2_749_900), valuation!.Amount);
        Assert.Equal(EvidenceStrength.SelfAsserted, valuation.Strength);
        Assert.Equal(AiBaselineValuationRail.RailSource, valuation.Source);
        Assert.Null(valuation.Reference);
    }

    [Fact]
    public void AiBaseline_returns_null_when_the_item_was_not_priced()
    {
        var rail = new AiBaselineValuationRail();

        Assert.Null(rail.Value(Item(value: null)));
    }

    [Fact]
    public void AiBaseline_values_every_category_in_v1()
    {
        var rail = new AiBaselineValuationRail();

        Assert.All(Enum.GetValues<AssetCategory>(), c => Assert.True(rail.CanValue(c)));
    }

    [Fact]
    public void Router_uses_the_first_rail_that_can_value_the_category()
    {
        var professional = new StubRail(
            canValue: c => c == AssetCategory.Jewellery,
            value: new Valuation(Money.Zar(80_000_00), EvidenceStrength.Independent, "valuer", "blob/cert.pdf"));
        var router = new ValuationRailRouter([professional, new AiBaselineValuationRail()]);

        var jewellery = router.Value(Item(Money.Zar(50_000_00), AssetCategory.Jewellery));
        var electronics = router.Value(Item(Money.Zar(2_749_900), AssetCategory.Electronics));

        Assert.Equal(EvidenceStrength.Independent, jewellery!.Strength);        // professional rail claimed it
        Assert.Equal(AiBaselineValuationRail.RailSource, electronics!.Source);  // baseline caught the rest
    }

    [Fact]
    public void Router_returns_null_when_no_rail_prices_the_item()
    {
        var router = new ValuationRailRouter([new AiBaselineValuationRail()]);

        Assert.Null(router.Value(Item(value: null)));
    }

    [Fact]
    public void ProfessionalRail_claims_only_jewellery_and_art()
    {
        var rail = new ProfessionalValuationRail();

        Assert.True(rail.CanValue(AssetCategory.Jewellery));
        Assert.True(rail.CanValue(AssetCategory.ArtAndCollectibles));
        Assert.False(rail.CanValue(AssetCategory.Electronics));
        Assert.False(rail.CanValue(AssetCategory.Furniture));
    }

    [Fact]
    public void ProfessionalRail_declines_to_auto_value_its_categories()
    {
        var rail = new ProfessionalValuationRail();

        Assert.Null(rail.Value(Item(Money.Zar(50_000_00), AssetCategory.Jewellery)));
    }

    [Fact]
    public void Router_in_registration_order_declines_jewellery_but_ai_prices_electronics()
    {
        // Mirrors the DI order: professional rail first, AI baseline second.
        var router = new ValuationRailRouter([new ProfessionalValuationRail(), new AiBaselineValuationRail()]);

        // Jewellery is claimed by the professional rail, which declines to auto-value it (no misleading estimate).
        Assert.Null(router.Value(Item(Money.Zar(50_000_00), AssetCategory.Jewellery)));

        // Electronics falls through to the AI baseline.
        var electronics = router.Value(Item(Money.Zar(2_749_900), AssetCategory.Electronics));
        Assert.Equal(AiBaselineValuationRail.RailSource, electronics!.Source);
    }

    private sealed class StubRail(Func<AssetCategory, bool> canValue, Valuation value) : IValuationRail
    {
        public bool CanValue(AssetCategory category) => canValue(category);
        public Valuation? Value(IdentifiedItem item) => value;
    }
}
