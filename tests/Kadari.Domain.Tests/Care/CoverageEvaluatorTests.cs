using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Tests.CareRegistry;

namespace Kadari.Domain.Tests.Care;

public class CoverageEvaluatorTests
{
    private static readonly DateTimeOffset Now = RuleFactory.Now;   // 24 Oct 2026

    private static CoverageInputs Inputs(
        BrandId? brand = null, CareCategory? category = CareCategory.MajorAppliance, string? model = null,
        RetailerId? retailer = null, DateOnly? purchased = null, PurchaseEvidence evidence = PurchaseEvidence.Independent,
        bool noBrand = false)
        => new("ZA", noBrand ? null : brand ?? RuleFactory.Bosch, category, model, retailer,
            purchased ?? new DateOnly(2026, 3, 14), evidence);

    [Fact]
    public void Evaluate_WithReceiptAndCurrentRule_IsActiveUntilPurchasePlusDuration()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(), [RuleFactory.Rule()], Now);

        Assert.Equal((CoverageState.Active, new DateOnly(2028, 3, 14)), (result.State, result.Primary!.ExpiresOn));
    }

    [Fact]
    public void Evaluate_WithNoMatchingRule_IsUnknown()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(brand: RuleFactory.Hisense), [RuleFactory.Rule()], Now);

        Assert.Equal(CoverageState.Unknown, result.State);
    }

    [Fact]
    public void Evaluate_WithUnknownBrand_IsUnknownAndRecordsNoRule()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(noBrand: true), [RuleFactory.Rule()], Now);

        Assert.Equal((CoverageState.Unknown, 0), (result.State, result.Findings.Count));
    }

    [Fact]
    public void Evaluate_IgnoresDraftAndSupersededRules()
    {
        var superseded = RuleFactory.Rule();
        superseded.Supersede(Now);

        var result = CoverageEvaluator.Evaluate(Inputs(), [RuleFactory.Rule(publish: false), superseded], Now);

        Assert.Equal(CoverageState.Unknown, result.State);
    }

    [Fact]
    public void Evaluate_PrefersTheModelRuleOverTheBrandRule()
    {
        var brand = RuleFactory.Rule(months: 24);
        var model = RuleFactory.Rule(model: "WGG244", months: 60);

        var result = CoverageEvaluator.Evaluate(Inputs(model: "wgg 244"), [brand, model], Now);

        Assert.Equal(model.Id, result.Primary!.RuleVersionId);
    }

    [Fact]
    public void Evaluate_WhenTheMostSpecificRulesDisagree_NeedsReviewInsteadOfPicking()
    {
        var a = RuleFactory.Rule(category: CareCategory.MajorAppliance, months: 24);
        var b = RuleFactory.Rule(category: CareCategory.MajorAppliance, months: 12);

        var result = CoverageEvaluator.Evaluate(Inputs(), [a, b], Now);

        Assert.Equal((CoverageState.NeedsReview, (CoverageRuleVersionId?)null), (result.State, result.Primary!.RuleVersionId));
    }

    [Fact]
    public void Evaluate_WithSelfAssertedPurchase_IsOnlyLikelyActive()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(evidence: PurchaseEvidence.SelfAsserted), [RuleFactory.Rule()], Now);

        Assert.Equal(CoverageState.LikelyActive, result.State);
    }

    [Fact]
    public void Evaluate_WithAStaleRule_IsOnlyLikelyActive()
    {
        var stale = RuleFactory.Rule(lastVerified: Now.AddDays(-400));

        var result = CoverageEvaluator.Evaluate(Inputs(), [stale], Now);

        Assert.Equal((CoverageState.LikelyActive, true), (result.State, result.Primary!.RuleStale));
    }

    [Fact]
    public void Evaluate_WithALowConfidenceRule_IsOnlyLikelyActive()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(), [RuleFactory.Rule(confidence: RegistryConfidence.Medium)], Now);

        Assert.Equal(CoverageState.LikelyActive, result.State);
    }

    [Fact]
    public void Evaluate_WhenRegistrationIsRequired_IsOnlyLikelyActive()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(), [RuleFactory.Rule(registrationRequired: true)], Now);

        Assert.Equal(CoverageState.LikelyActive, result.State);
    }

    [Fact]
    public void Evaluate_WithoutAPurchaseDate_IsLikelyActiveWithNoDates()
    {
        var inputs = Inputs() with { PurchasedOn = null };

        var result = CoverageEvaluator.Evaluate(inputs, [RuleFactory.Rule()], Now);

        Assert.Equal((CoverageState.LikelyActive, (DateOnly?)null), (result.State, result.Primary!.ExpiresOn));
    }

    [Fact]
    public void Evaluate_WithinNinetyDaysOfExpiry_IsExpiring()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(purchased: new DateOnly(2024, 12, 1)), [RuleFactory.Rule()], Now);

        Assert.Equal(CoverageState.Expiring, result.State);
    }

    [Fact]
    public void Evaluate_AfterExpiryOnStrongEvidence_IsExpired()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(purchased: new DateOnly(2023, 1, 10)), [RuleFactory.Rule()], Now);

        Assert.Equal(CoverageState.Expired, result.State);
    }

    [Fact]
    public void Evaluate_AfterExpiryOnWeakEvidence_IsUnknownNotExpired()
    {
        var inputs = Inputs(purchased: new DateOnly(2023, 1, 10), evidence: PurchaseEvidence.SelfAsserted);

        var result = CoverageEvaluator.Evaluate(inputs, [RuleFactory.Rule()], Now);

        Assert.Equal(CoverageState.Unknown, result.State);
    }

    [Fact]
    public void Evaluate_ClampsAMonthEndPurchase()
    {
        var result = CoverageEvaluator.Evaluate(Inputs(purchased: new DateOnly(2026, 8, 31)), [RuleFactory.Rule(months: 6)], Now);

        Assert.Equal(new DateOnly(2027, 2, 28), result.Primary!.ExpiresOn);
    }

    [Fact]
    public void Evaluate_ARuleForAnotherCountry_DoesNotMatch()
    {
        var result = CoverageEvaluator.Evaluate(Inputs() with { CountryCode = "GB" }, [RuleFactory.Rule()], Now);

        Assert.Equal(CoverageState.Unknown, result.State);
    }

    [Fact]
    public void Evaluate_HeadlinesTheBestCoverWhenManufacturerHasExpired()
    {
        var manufacturer = RuleFactory.Rule(months: 12);
        var extended = RuleFactory.Rule(months: 60, type: CoverageType.Extended);

        var result = CoverageEvaluator.Evaluate(Inputs(purchased: new DateOnly(2024, 1, 10)), [manufacturer, extended], Now);

        Assert.Equal((CoverageState.Active, CoverageType.Extended), (result.State, result.Primary!.CoverageType));
    }

    [Fact]
    public void Evaluate_IsDeterministic()
    {
        var rules = new[] { RuleFactory.Rule(), RuleFactory.Rule(model: "WGG244", months: 36) };

        var first = CoverageEvaluator.Evaluate(Inputs(model: "WGG244"), rules, Now);
        var second = CoverageEvaluator.Evaluate(Inputs(model: "WGG244"), rules.Reverse(), Now);

        Assert.Equal(first, second with { Findings = first.Findings });
    }

    [Fact]
    public void Evaluate_WhenRegistrationIsRequiredAndCompleted_IsActive()
    {
        var inputs = Inputs() with { RegisteredOn = new DateOnly(2026, 3, 20) };

        var result = CoverageEvaluator.Evaluate(inputs, [RuleFactory.Rule(registrationRequired: true)], Now);

        Assert.Equal(CoverageState.Active, result.State);
    }

    [Fact]
    public void Evaluate_AfterRegistration_AddsTheExtensionMonths()
    {
        var rule = CoverageRuleVersion.Draft(
            new CoverageRuleVersionId(Guid.CreateVersion7()), Guid.CreateVersion7(), 1, "ZA", RuleFactory.Bosch, null, null,
            null, CoverageType.Manufacturer, 24, CoverageStartBasis.PurchaseDate, false, null, 12,
            "Two years, three when registered.", null, null, RegistryConfidence.High, RuleFactory.Source, Now.AddDays(-10));
        rule.Publish(Now.AddDays(-1));

        var registered = CoverageEvaluator.Evaluate(Inputs() with { RegisteredOn = new DateOnly(2026, 3, 20) }, [rule], Now);
        var unregistered = CoverageEvaluator.Evaluate(Inputs(), [rule], Now);

        Assert.Equal(
            (new DateOnly(2029, 3, 14), new DateOnly(2028, 3, 14)),
            (registered.Primary!.ExpiresOn, unregistered.Primary!.ExpiresOn));
    }
}
