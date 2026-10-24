using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Owners;

namespace Kadari.Application.Tests.Features.Care;

/// <summary>The Care card (CARE/05 C01, §3): one plain-language headline and one next action per state, and honest
/// gaps — never an invented date.</summary>
public class CareCardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static Asset AnAsset(AssetCategory category = AssetCategory.Electronics) =>
        Asset.Register(TenantId.Default, OwnerId.New(), "Bosch washing machine", category, Now);

    private static CareProfile ProfileIn(Asset asset, CoverageState state, DateOnly? expires = null)
    {
        var profile = CareProfile.Enable(asset.Id, CareCategory.MajorAppliance, Now);
        if (state == CoverageState.Unknown)
        {
            return profile;
        }

        var finding = new CoverageFinding(CoverageType.Manufacturer, state, null, null, null, false, 0, null, expires, false,
            null, null, "test");
        var evaluation = new CoverageEvaluation(state, [finding], "test");
        profile.ApplyEvaluation(evaluation, CoverageDecision.Record(asset.Id, evaluation, "{}", Now), Now);
        return profile;
    }

    [Fact]
    public void For_ARelevantAssetWithoutCare_InvitesTheOwnerToWorkOutTheirCover()
    {
        var card = CareCard.For(AnAsset(), profile: null);

        Assert.Equal((CareCardState.NotEnabled, CarePrimaryAction.WorkOutMyCover), (card.State, card.PrimaryAction));
    }

    [Fact]
    public void For_Jewellery_ShowsNoCard()
    {
        var card = CareCard.For(AnAsset(AssetCategory.Jewellery), profile: null);

        Assert.Equal((CareCardState.NotCareRelevant, CarePrimaryAction.None), (card.State, card.PrimaryAction));
    }

    [Fact]
    public void For_ActiveCover_SaysUntilWhenAndOffersTheCoverage()
    {
        var asset = AnAsset();

        var card = CareCard.For(asset, ProfileIn(asset, CoverageState.Active, new DateOnly(2028, 3, 14)));

        Assert.Equal(("Covered until 14 March 2028.", CarePrimaryAction.ViewCoverage), (card.Detail, card.PrimaryAction));
    }

    [Fact]
    public void For_ExpiringCover_AsksTheOwnerToCheckTheProduct()
    {
        var asset = AnAsset();

        var card = CareCard.For(asset, ProfileIn(asset, CoverageState.Expiring, new DateOnly(2026, 12, 1)));

        Assert.Equal((CareCardState.Expiring, CarePrimaryAction.CheckMyProduct), (card.State, card.PrimaryAction));
    }

    [Fact]
    public void For_UnknownCoverWithNothingRecorded_ListsWhatIsMissing()
    {
        var asset = AnAsset();

        var card = CareCard.For(asset, ProfileIn(asset, CoverageState.Unknown));

        Assert.Equal(
            [CareEvidenceGap.Brand, CareEvidenceGap.PurchaseDate, CareEvidenceGap.ProofOfPurchase],
            card.Gaps);
    }

    [Fact]
    public void For_NeedsReview_PromisesAPersonRatherThanAGuess()
    {
        var asset = AnAsset();

        var card = CareCard.For(asset, ProfileIn(asset, CoverageState.NeedsReview));

        Assert.Equal(CareCardState.AttentionRequired, card.State);
    }
}
