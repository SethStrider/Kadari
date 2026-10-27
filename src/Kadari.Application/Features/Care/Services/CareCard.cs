using Kadari.Domain.Assets;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

/// <summary>The Digital Twin Care card states (CARE/05 C01) this build can reach. Registration and case states arrive
/// with their epics.</summary>
public enum CareCardState
{
    NotCareRelevant,
    NotEnabled,
    Unknown,
    Active,
    Expiring,
    Expired,
    CaseOpen,
    AttentionRequired,
    RegistrationAvailable,
    RegistrationInProgress,
}

/// <summary>The one primary action the card offers (CARE/05 C01 "primary CTA by state").</summary>
public enum CarePrimaryAction
{
    None,
    WorkOutMyCover,
    AddPurchaseDetails,
    AddProofOfPurchase,
    ViewCoverage,
    CheckMyProduct,
    ViewCase,
    ActivateExtraCover,
    FinishRegistration,
}

/// <summary>What is missing for a confident answer (CARE/13 §2 evidenceGaps).</summary>
public enum CareEvidenceGap
{
    Brand,
    PurchaseDate,
    ProofOfPurchase,
}

public sealed record CareCardView(
    CareCardState State, string Headline, string Detail, CarePrimaryAction PrimaryAction, IReadOnlyList<CareEvidenceGap> Gaps);

/// <summary>
/// Turns the Care profile and the asset's evidence into what the card says (CARE/05 C01, §3 global states). Pure. The
/// copy answers the customer's questions in plain language and never shows internal enums, rule confidence or
/// registry names (CARE/16 §7).
/// </summary>
public static class CareCard
{
    /// <summary>The insurance categories where after-sales Care is not the point. They degrade to no card at all rather
    /// than an empty one; the owner can still switch Care on explicitly.</summary>
    private static readonly AssetCategory[] NotCareRelevant =
        [AssetCategory.Clothing, AssetCategory.Jewellery, AssetCategory.ArtAndCollectibles];

    public static bool IsCareRelevant(AssetCategory category) => !NotCareRelevant.Contains(category);

    /// <param name="openOffer">An extra-cover offer the owner can still take, if any.</param>
    /// <param name="openRegistration">A registration waiting for the owner's step, if any.</param>
    public static CareCardView For(
        Asset asset, CareProfile? profile, RegistrationOpportunity? openOffer = null, CoverageRegistration? openRegistration = null)
    {
        if (profile is null)
        {
            return IsCareRelevant(asset.Category)
                ? new CareCardView(CareCardState.NotEnabled, "Let's work out your cover",
                    "Add a receipt or the purchase details and KADARI will work out the important dates and next steps.",
                    CarePrimaryAction.WorkOutMyCover, [])
                : new CareCardView(CareCardState.NotCareRelevant, "", "", CarePrimaryAction.None, []);
        }

        var gaps = GapsFor(asset);

        if (profile.OpenCaseCount > 0)
        {
            return new CareCardView(CareCardState.CaseOpen, "Case open", "KADARI is tracking this for you.",
                CarePrimaryAction.ViewCase, gaps);
        }

        var expires = profile.CoverageExpiresOn is { } date ? date.ToString("d MMMM yyyy") : null;

        // Activation outranks the coverage headline: it is the one thing the owner can do now that improves their
        // cover, and it may have a deadline (CARE/05 C01 REGISTRATION_AVAILABLE).
        if (openRegistration is not null)
        {
            return new CareCardView(CareCardState.RegistrationInProgress, "Finish your registration",
                "KADARI has everything ready. Complete the last step on the official site, then tell KADARI the reference.",
                CarePrimaryAction.FinishRegistration, gaps);
        }

        if (openOffer is not null)
        {
            var deadline = openOffer.DeadlineOn is { } by ? $" Register by {by:d MMMM yyyy}." : "";
            return new CareCardView(CareCardState.RegistrationAvailable, "Extra cover available",
                $"{openOffer.BenefitSummary}{deadline}", CarePrimaryAction.ActivateExtraCover, gaps);
        }

        return profile.CoverageState switch
        {
            CoverageState.Active => new CareCardView(CareCardState.Active, "Under warranty",
                $"Covered until {expires}.", CarePrimaryAction.ViewCoverage, gaps),

            CoverageState.Expiring => new CareCardView(CareCardState.Expiring, "Warranty ends soon",
                $"Your cover ends on {expires}. If you've noticed anything unusual, now is the time to check it.",
                CarePrimaryAction.CheckMyProduct, gaps),

            CoverageState.Expired => new CareCardView(CareCardState.Expired, "Warranty ended",
                $"The manufacturer's cover ended on {expires}.", CarePrimaryAction.ViewCoverage, gaps),

            CoverageState.LikelyActive => new CareCardView(CareCardState.Active, "Likely warranty found",
                expires is null
                    ? "Add the purchase date so KADARI can work out the dates."
                    : $"Probably covered until {expires}. Add your proof of purchase so KADARI can confirm it.",
                gaps.Contains(CareEvidenceGap.PurchaseDate) ? CarePrimaryAction.AddPurchaseDetails : CarePrimaryAction.AddProofOfPurchase,
                gaps),

            CoverageState.NeedsReview => new CareCardView(CareCardState.AttentionRequired, "KADARI is checking this one",
                "We won't guess. A person will confirm the cover for this product.", CarePrimaryAction.ViewCoverage, gaps),

            _ => new CareCardView(CareCardState.Unknown, "KADARI hasn't confirmed this yet",
                gaps.Count > 0
                    ? "We won't guess. Add more information and KADARI will look again."
                    : "We won't guess. KADARI doesn't have a verified rule for this product yet.",
                gaps.Count > 0 ? CarePrimaryAction.AddPurchaseDetails : CarePrimaryAction.ViewCoverage, gaps),
        };
    }

    private static List<CareEvidenceGap> GapsFor(Asset asset)
    {
        var facts = asset.LatestPurchase?.Purchase;
        var gaps = new List<CareEvidenceGap>();

        if (facts?.Brand is null)
        {
            gaps.Add(CareEvidenceGap.Brand);
        }

        if (facts?.PurchasedOn is null)
        {
            gaps.Add(CareEvidenceGap.PurchaseDate);
        }

        var receiptHeld = asset.Evidence.Any(e =>
            e.Kind == AssetEvidenceKind.Ownership && e.Strength == EvidenceStrength.Independent && e.Source == "receipt");
        if (!receiptHeld && asset.LatestPurchase?.Strength != EvidenceStrength.Independent)
        {
            gaps.Add(CareEvidenceGap.ProofOfPurchase);
        }

        return gaps;
    }
}
