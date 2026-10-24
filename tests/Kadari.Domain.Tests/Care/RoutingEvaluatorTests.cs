using Kadari.Domain.Abstractions;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Tests.CareRegistry;

namespace Kadari.Domain.Tests.Care;

public class RoutingEvaluatorTests
{
    private static readonly DateTimeOffset Now = RuleFactory.Now;

    private static RoutingInputs Inputs(
        BrandId? brand = null, CareCategory? category = CareCategory.MajorAppliance, string? model = null,
        RetailerId? retailer = null, CoverageState coverage = CoverageState.Active, IssueClass issue = IssueClass.Fault)
        => new("ZA", brand ?? RuleFactory.Bosch, category, model, retailer, coverage, issue);

    private static RoutingRuleVersion Rule(
        RouteType route = RouteType.OemDirect, BrandId? brand = null, CareCategory? category = null, string? model = null,
        RetailerId? retailer = null, RouteCoverage coverage = RouteCoverage.Any, IssueClass issue = IssueClass.Any,
        int priority = 100, RegistryConfidence confidence = RegistryConfidence.High, DateTimeOffset? lastVerified = null,
        bool publish = true, bool noBrand = false) =>
        RoutingRuleFactory.Rule(route, brand, category, model, retailer, coverage, issue, priority, confidence, lastVerified,
            publish, noBrand);

    [Fact]
    public void Evaluate_WithAConfidentCurrentRule_IsGreenAndCitesTheRule()
    {
        var rule = Rule();

        var result = RoutingEvaluator.Evaluate(Inputs(), [rule], Now);

        Assert.Equal((RouteType.OemDirect, RouteConfidence.Green, (RoutingRuleVersionId?)rule.Id),
            (result.RouteType, result.Confidence, result.RuleVersionId));
    }

    [Fact]
    public void Evaluate_WithNoMatchingRule_IsResearchRequiredAndRed()
    {
        var result = RoutingEvaluator.Evaluate(Inputs(brand: RuleFactory.Hisense), [Rule()], Now);

        Assert.Equal((RouteType.ResearchRequired, RouteConfidence.Red, (RoutingRuleVersionId?)null),
            (result.RouteType, result.Confidence, result.RuleVersionId));
    }

    [Fact]
    public void Evaluate_WhenEqualRulesDisagree_DoesNotPick()
    {
        var result = RoutingEvaluator.Evaluate(Inputs(), [Rule(RouteType.OemDirect), Rule(RouteType.RetailerReturn)], Now);

        Assert.Equal((RouteType.ResearchRequired, RouteConfidence.Red, 2), (result.RouteType, result.Confidence, result.CompetingRoutes));
    }

    [Fact]
    public void Evaluate_PrefersTheModelRuleOverTheBrandRule()
    {
        var model = Rule(RouteType.AuthorisedService, model: "WGG244");

        var result = RoutingEvaluator.Evaluate(Inputs(model: "wgg 244"), [Rule(RouteType.OemDirect), model], Now);

        Assert.Equal(model.Id, result.RuleVersionId);
    }

    [Fact]
    public void Evaluate_PrefersTheBrandCategoryRuleOverTheBrandRule()
    {
        var narrower = Rule(RouteType.Onsite, category: CareCategory.MajorAppliance);

        var result = RoutingEvaluator.Evaluate(Inputs(), [Rule(RouteType.CarryIn), narrower], Now);

        Assert.Equal(RouteType.Onsite, result.RouteType);
    }

    [Fact]
    public void Evaluate_AtEqualSpecificity_TheLowerPriorityNumberWins()
    {
        var first = Rule(RouteType.RetailerReturn, priority: 10);

        var result = RoutingEvaluator.Evaluate(Inputs(), [Rule(RouteType.OemDirect, priority: 20), first], Now);

        Assert.Equal(first.Id, result.RuleVersionId);
    }

    [Fact]
    public void Evaluate_AnInCoverRule_DoesNotApplyOutOfCover()
    {
        var inCover = Rule(RouteType.OemDirect, coverage: RouteCoverage.InCover);
        var outOfCover = Rule(RouteType.AuthorisedService, coverage: RouteCoverage.OutOfCover);

        var result = RoutingEvaluator.Evaluate(Inputs(coverage: CoverageState.Expired), [inCover, outOfCover], Now);

        Assert.Equal(RouteType.AuthorisedService, result.RouteType);
    }

    [Fact]
    public void Evaluate_ACoverDependentRule_NeverMatchesWhileCoverIsUnknown()
    {
        var result = RoutingEvaluator.Evaluate(
            Inputs(coverage: CoverageState.Unknown), [Rule(coverage: RouteCoverage.InCover)], Now);

        Assert.Equal(RouteType.ResearchRequired, result.RouteType);
    }

    [Fact]
    public void Evaluate_ACoverDependentRuleOnLikelyCover_IsAmber()
    {
        var result = RoutingEvaluator.Evaluate(
            Inputs(coverage: CoverageState.LikelyActive), [Rule(coverage: RouteCoverage.InCover)], Now);

        Assert.Equal((RouteType.OemDirect, RouteConfidence.Amber), (result.RouteType, result.Confidence));
    }

    [Fact]
    public void Evaluate_AnIssueSpecificRule_OnlyMatchesThatIssue()
    {
        var doa = Rule(RouteType.RetailerReturn, issue: IssueClass.DeadOnArrival);

        var result = RoutingEvaluator.Evaluate(Inputs(issue: IssueClass.AccidentalDamage), [doa], Now);

        Assert.Equal(RouteType.ResearchRequired, result.RouteType);
    }

    [Fact]
    public void Evaluate_WithAStaleRule_IsAmber()
    {
        var result = RoutingEvaluator.Evaluate(Inputs(), [Rule(lastVerified: Now.AddDays(-400))], Now);

        Assert.Equal(RouteConfidence.Amber, result.Confidence);
    }

    [Fact]
    public void Evaluate_WithAMediumConfidenceRule_IsAmber()
    {
        var result = RoutingEvaluator.Evaluate(Inputs(), [Rule(confidence: RegistryConfidence.Medium)], Now);

        Assert.Equal(RouteConfidence.Amber, result.Confidence);
    }

    [Fact]
    public void Evaluate_IgnoresDraftAndSupersededRules()
    {
        var superseded = Rule();
        superseded.Supersede(Now);

        var result = RoutingEvaluator.Evaluate(Inputs(), [Rule(publish: false), superseded], Now);

        Assert.Equal(RouteType.ResearchRequired, result.RouteType);
    }

    [Fact]
    public void Evaluate_ARetailerRule_MatchesOnlyThatRetailerAndCategory()
    {
        var makro = Rule(RouteType.RetailerCredit, retailer: RuleFactory.Makro, category: CareCategory.SmallAppliance, noBrand: true);

        var result = RoutingEvaluator.Evaluate(
            Inputs(brand: RuleFactory.Hisense, category: CareCategory.SmallAppliance, retailer: RuleFactory.Makro), [makro], Now);

        Assert.Equal(RouteType.RetailerCredit, result.RouteType);
    }

    [Fact]
    public void Evaluate_IsDeterministic()
    {
        var rules = new[] { Rule(RouteType.OemDirect, priority: 5), Rule(RouteType.CarryIn, category: CareCategory.MajorAppliance) };

        var first = RoutingEvaluator.Evaluate(Inputs(), rules, Now);
        var second = RoutingEvaluator.Evaluate(Inputs(), rules.Reverse(), Now);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Draft_ResearchRequired_IsNotARule()
    {
        Assert.Throws<DomainException>(() => Rule(RouteType.ResearchRequired, publish: false));
    }

    [Fact]
    public void Draft_AWebPageContactOverHttp_IsRefused()
    {
        Assert.Throws<DomainException>(() => RoutingRuleFactory.Rule(publish: false, channel: "http://example.com/support"));
    }

    [Fact]
    public void Draft_WithoutBrandOrRetailerAndCategory_IsRefused()
    {
        Assert.Throws<DomainException>(() => Rule(noBrand: true, publish: false));
    }
}

public class RoutingDecisionTests
{
    [Fact]
    public void Override_IsMarkedAsAPersonsDecisionWithItsReason()
    {
        var decision = RoutingDecision.Override(
            new Kadari.Domain.Assets.AssetId(Guid.CreateVersion7()), RouteType.AuthorisedService, "Called Bosch ZA", "{}", RuleFactory.Now);

        Assert.Equal((true, "Called Bosch ZA", (RoutingRuleVersionId?)null),
            (decision.HumanOverride, decision.HumanOverrideReason, decision.MatchedRuleVersionId));
    }

    [Fact]
    public void Override_ToResearchRequired_IsRefused()
    {
        Assert.Throws<DomainException>(() => RoutingDecision.Override(
            new Kadari.Domain.Assets.AssetId(Guid.CreateVersion7()), RouteType.ResearchRequired, "unsure", "{}", RuleFactory.Now));
    }
}

public class SafetyGateTests
{
    [Theory]
    [InlineData("There is smoke coming from the back")]
    [InlineData("It smells of gas near the hob")]
    [InlineData("The phone battery is swollen")]
    [InlineData("It gave me an ELECTRIC SHOCK")]
    public void Trips_OnASafetySignal(string description)
    {
        Assert.True(SafetyGate.Trips(description, ownerSaysUnsafe: null));
    }

    [Fact]
    public void Trips_WhenTheOwnerSaysItIsUnsafe()
    {
        Assert.True(SafetyGate.Trips("It just stopped", ownerSaysUnsafe: true));
    }

    [Fact]
    public void Trips_NotOnAnOrdinaryFault()
    {
        Assert.False(SafetyGate.Trips("The drum does not spin and it shows E18", ownerSaysUnsafe: false));
    }
}
