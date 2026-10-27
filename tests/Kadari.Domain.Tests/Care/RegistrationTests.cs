using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Tests.CareRegistry;

namespace Kadari.Domain.Tests.Care;

/// <summary>Registration (CARE/07 §6, CARE/14 C5): explicit authority, proof before "completed", no second chance at a
/// taken offer, external failure kept open or handed to a person.</summary>
public class RegistrationTests
{
    private static readonly DateTimeOffset Now = RuleFactory.Now;   // 24 Oct 2026
    private static readonly AssetId Asset = AssetId.New();

    private static RegistrationRuleVersion Rule(RegistrationMode mode = RegistrationMode.Guided, int? deadlineDays = 28)
    {
        var rule = RegistrationRuleVersion.Draft(
            new RegistrationRuleVersionId(Guid.CreateVersion7()), Guid.CreateVersion7(), 1, "ZA", RuleFactory.Bosch, null, null,
            mode, RegistrationDestinationType.WebPage, "https://www.bosch-home.com/za/mybosch",
            [RegistrationField.OwnerName, RegistrationField.SerialNumber], deadlineDays, 24,
            "Register within 28 days to add two years.", null, RegistryConfidence.High, RuleFactory.Source, Now.AddDays(-5));
        rule.Publish(Now.AddDays(-1));
        return rule;
    }

    private static ExternalActionAuthorisation Authority(AssetId? asset = null) =>
        ExternalActionAuthorisation.Grant(Guid.CreateVersion7(), asset ?? Asset, ExternalActionType.WarrantyRegistration, "{}", "v1", Now);

    private static CoverageRegistration Started(RegistrationMode mode = RegistrationMode.Guided)
    {
        var rule = Rule(mode);
        var opportunity = RegistrationOpportunity.Surface(Asset, rule, new DateOnly(2026, 10, 10), Now);
        return CoverageRegistration.Start(opportunity, rule, Authority(), "key-1", Now);
    }

    [Fact]
    public void Surface_SetsTheDeadlineFromThePurchaseDate()
    {
        var opportunity = RegistrationOpportunity.Surface(Asset, Rule(), new DateOnly(2026, 10, 10), Now);

        Assert.Equal((OpportunityStatus.Available, new DateOnly(2026, 11, 7)), (opportunity.Status, opportunity.DeadlineOn));
    }

    [Fact]
    public void Surface_PastTheDeadline_IsExpired()
    {
        var opportunity = RegistrationOpportunity.Surface(Asset, Rule(), new DateOnly(2026, 1, 10), Now);

        Assert.Equal(OpportunityStatus.Expired, opportunity.Status);
    }

    [Fact]
    public void Refresh_DoesNotReopenADismissedOffer()
    {
        var rule = Rule();
        var opportunity = RegistrationOpportunity.Surface(Asset, rule, new DateOnly(2026, 10, 10), Now);
        opportunity.Dismiss(Now);

        opportunity.Refresh(rule, new DateOnly(2026, 10, 12), Now);

        Assert.Equal(OpportunityStatus.Dismissed, opportunity.Status);
    }

    [Fact]
    public void Start_TakesTheOffer_SoItCannotBeTakenAgain()
    {
        var rule = Rule();
        var opportunity = RegistrationOpportunity.Surface(Asset, rule, new DateOnly(2026, 10, 10), Now);
        CoverageRegistration.Start(opportunity, rule, Authority(), "key-1", Now);

        Assert.Throws<DomainException>(() => CoverageRegistration.Start(opportunity, rule, Authority(), "key-2", Now));
    }

    [Fact]
    public void Start_WithAuthorityForAnotherAsset_Throws()
    {
        var rule = Rule();
        var opportunity = RegistrationOpportunity.Surface(Asset, rule, new DateOnly(2026, 10, 10), Now);

        Assert.Throws<DomainException>(() => CoverageRegistration.Start(opportunity, rule, Authority(AssetId.New()), "key", Now));
    }

    [Fact]
    public void Start_WithRevokedAuthority_Throws()
    {
        var rule = Rule();
        var opportunity = RegistrationOpportunity.Surface(Asset, rule, new DateOnly(2026, 10, 10), Now);
        var authority = Authority();
        authority.Revoke(Now);

        Assert.Throws<DomainException>(() => CoverageRegistration.Start(opportunity, rule, authority, "key", Now));
    }

    [Fact]
    public void Start_AnAutomaticRule_RunsAsAssistedInV1()
        => Assert.Equal(RegistrationMode.Assisted, Started(RegistrationMode.Automatic).Mode);

    [Fact]
    public void Complete_WithoutAReference_Throws()
        => Assert.Throws<ArgumentException>(() => Started().Complete("  ", new DateOnly(2026, 10, 20), Now));

    [Fact]
    public void Complete_WithAReference_IsCompletedAndRaisesTheEvent()
    {
        var registration = Started();

        registration.Complete("MB-123456", new DateOnly(2026, 10, 20), Now);

        Assert.Equal(
            (RegistrationStatus.Completed, true),
            (registration.Status, registration.DomainEvents.Any(e => e is Kadari.Domain.Care.Events.WarrantyRegistrationCompletedDomainEvent)));
    }

    [Fact]
    public void Complete_Twice_Throws()
    {
        var registration = Started();
        registration.Complete("MB-1", new DateOnly(2026, 10, 20), Now);

        Assert.Throws<DomainException>(() => registration.Complete("MB-2", new DateOnly(2026, 10, 21), Now));
    }

    [Fact]
    public void Fail_WhenTheProviderWasDown_StaysOpenToRetry()
    {
        var registration = Started();

        registration.Fail("The site would not load.", retryable: true, Now);

        Assert.Equal((RegistrationStatus.FailedRetryable, true), (registration.Status, registration.IsOpen));
    }

    [Fact]
    public void Fail_ForAnyOtherReason_NeedsAPerson()
    {
        var registration = Started();

        registration.Fail("The form rejected my serial.", retryable: false, Now);

        Assert.Equal((RegistrationStatus.FailedManual, false), (registration.Status, registration.IsOpen));
    }

    [Fact]
    public void Draft_WithAPlainHttpDestination_Throws()
        => Assert.Throws<DomainException>(() => RegistrationRuleVersion.Draft(
            new RegistrationRuleVersionId(Guid.CreateVersion7()), Guid.CreateVersion7(), 1, "ZA", RuleFactory.Bosch, null, null,
            RegistrationMode.Guided, RegistrationDestinationType.WebPage, "http://example.com", [], null, null, "x", null,
            RegistryConfidence.High, RuleFactory.Source, Now));
}
