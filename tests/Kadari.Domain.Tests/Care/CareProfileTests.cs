using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.Care.Events;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Tests.CareRegistry;

namespace Kadari.Domain.Tests.Care;

public class CareProfileTests
{
    private static readonly DateTimeOffset Now = RuleFactory.Now;
    private static readonly AssetId Asset = AssetId.New();

    private static CoverageInputs Inputs(DateOnly purchased) =>
        new("ZA", RuleFactory.Bosch, CareCategory.MajorAppliance, null, null, purchased, PurchaseEvidence.Independent);

    private static (CoverageEvaluation, CoverageDecision) Evaluate(AssetId asset, DateOnly purchased, params CoverageRuleVersion[] rules)
    {
        var evaluation = CoverageEvaluator.Evaluate(Inputs(purchased), rules, Now);
        return (evaluation, CoverageDecision.Record(asset, evaluation, "{}", Now));
    }

    [Fact]
    public void Enable_StartsUnknownAndRaisesCareEnabled()
    {
        var profile = CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now);

        Assert.Equal(
            (CoverageState.Unknown, true),
            (profile.CoverageState, profile.DomainEvents.Single() is CareEnabledDomainEvent));
    }

    [Fact]
    public void ApplyEvaluation_RecordsCoverageWithItsDecisionAndRule()
    {
        var profile = CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now);
        var rule = RuleFactory.Rule();
        var (evaluation, decision) = Evaluate(Asset, new DateOnly(2026, 3, 14), rule);

        profile.ApplyEvaluation(evaluation, decision, Now);

        var record = profile.Coverage.Single();
        Assert.Equal((CoverageState.Active, rule.Id, decision.Id), (record.State, record.RuleVersionId, record.DecisionId));
    }

    [Fact]
    public void ApplyEvaluation_SetsNextAttentionToNinetyDaysBeforeExpiry()
    {
        var profile = CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now);
        var (evaluation, decision) = Evaluate(Asset, new DateOnly(2026, 3, 14), RuleFactory.Rule());

        profile.ApplyEvaluation(evaluation, decision, Now);

        Assert.Equal(new DateOnly(2028, 3, 14).AddDays(-90), profile.NextAttentionOn);
    }

    [Fact]
    public void ApplyEvaluation_WhenARuleStopsMatching_DropsTheRecordToUnknownWithoutDeletingIt()
    {
        var profile = CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now);
        var (first, firstDecision) = Evaluate(Asset, new DateOnly(2026, 3, 14), RuleFactory.Rule());
        profile.ApplyEvaluation(first, firstDecision, Now);
        var (second, secondDecision) = Evaluate(Asset, new DateOnly(2026, 3, 14));

        profile.ApplyEvaluation(second, secondDecision, Now);

        Assert.Equal(CoverageState.Unknown, profile.Coverage.Single().State);
    }

    [Fact]
    public void ApplyEvaluation_RaisesAMaterialChangeOnlyWhenTheStateOrExpiryMoves()
    {
        var profile = CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now);
        var rule = RuleFactory.Rule();
        var (first, firstDecision) = Evaluate(Asset, new DateOnly(2026, 3, 14), rule);
        profile.ApplyEvaluation(first, firstDecision, Now);
        profile.ClearDomainEvents();
        var (again, againDecision) = Evaluate(Asset, new DateOnly(2026, 3, 14), rule);

        profile.ApplyEvaluation(again, againDecision, Now);

        Assert.False(((CoverageEvaluatedDomainEvent)profile.DomainEvents.Single()).IsMaterialChange);
    }

    [Fact]
    public void ApplyEvaluation_ForAnotherAssetsDecision_Throws()
    {
        var profile = CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now);
        var (evaluation, decision) = Evaluate(AssetId.New(), new DateOnly(2026, 3, 14), RuleFactory.Rule());

        Assert.Throws<DomainException>(() => profile.ApplyEvaluation(evaluation, decision, Now));
    }

    [Fact]
    public void Decision_RecordsTheExactRuleVersionsThatMatched()
    {
        var rule = RuleFactory.Rule();

        var (_, decision) = Evaluate(Asset, new DateOnly(2026, 3, 14), rule);

        Assert.Equal([rule.Id.Value], decision.MatchedRuleVersionIds);
    }
}
