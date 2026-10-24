using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.Care.Events;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Tests.CareRegistry;

namespace Kadari.Domain.Tests.Care;

public class AssetCaseTests
{
    private static readonly DateTimeOffset Now = RuleFactory.Now;
    private static readonly AssetId Asset = new(Guid.Parse("0192f000-0000-7000-8000-0000000000c1"));

    private static AssetCase Open(bool safety = false, CoverageState coverage = CoverageState.Active) =>
        AssetCase.Open(Asset, IssueClass.Fault, "The drum does not spin.\nIt shows E18.", safety, coverage, Now);

    private static (AssetCase Case, RoutingRuleVersion Rule) Routed(RouteConfidence confidence = RouteConfidence.Green)
    {
        var @case = Open();
        var rule = RoutingRuleFactory.Rule(confidence: confidence == RouteConfidence.Green ? RegistryConfidence.High : RegistryConfidence.Medium);
        var result = RoutingEvaluator.Evaluate(new RoutingInputs("ZA", RuleFactory.Bosch, null, null, null, CoverageState.Active, IssueClass.Fault), [rule], Now);
        @case.ApplyRoute(RoutingDecision.Record(Asset, result, "{}", Now), rule, Now);
        return (@case, rule);
    }

    private static AssetCase Sent()
    {
        var (@case, _) = Routed();
        @case.MarkSent("WebPage", "Brand, model, serial, purchase date, issue", "key-1", Now);
        return @case;
    }

    [Fact]
    public void Open_StartsPreparingWithNoRouteAndTitlesFromTheFirstLine()
    {
        var @case = Open();

        Assert.Equal((CaseStatus.Preparing, true, "The drum does not spin."), (@case.Status, @case.NeedsRoute, @case.Title));
    }

    [Fact]
    public void Open_InCover_IsAWarrantyCase()
    {
        Assert.Equal(CaseType.Warranty, Open(coverage: CoverageState.Active).CaseType);
    }

    [Fact]
    public void Open_OutOfCover_IsARepairCase()
    {
        Assert.Equal(CaseType.Repair, Open(coverage: CoverageState.Expired).CaseType);
    }

    [Fact]
    public void Open_WhenTheSafetyGateTripped_PutsTheSafetyStopOnTheTimeline()
    {
        var @case = Open(safety: true);

        Assert.Equal([CaseEventType.Opened, CaseEventType.SafetyStop], @case.Events.Select(e => e.Type));
    }

    [Fact]
    public void Open_RaisesAssetCaseOpened()
    {
        Assert.Contains(Open().DomainEvents, e => e is AssetCaseOpenedDomainEvent);
    }

    [Fact]
    public void ApplyRoute_Green_IsAcceptedWithTheRulesCounterparty()
    {
        var (@case, rule) = Routed();

        Assert.Equal((true, RouteType.OemDirect, rule.TargetName), (@case.RouteAccepted, @case.RouteType, @case.TargetName));
    }

    [Fact]
    public void ApplyRoute_Amber_WaitsForTheOwner()
    {
        var (@case, _) = Routed(RouteConfidence.Amber);

        Assert.False(@case.RouteAccepted);
    }

    [Fact]
    public void ApplyRoute_ResearchRequired_RaisesNeedsRoute()
    {
        var @case = Open();
        var result = RoutingEvaluator.Evaluate(new RoutingInputs("ZA", null, null, null, null, CoverageState.Unknown, IssueClass.Fault), [], Now);

        @case.ApplyRoute(RoutingDecision.Record(Asset, result, "{}", Now), null, Now);

        Assert.Contains(@case.DomainEvents, e => e is CaseNeedsRouteDomainEvent);
    }

    [Fact]
    public void MarkSent_BeforeTheRouteIsAccepted_IsRefused()
    {
        var (@case, _) = Routed(RouteConfidence.Amber);

        Assert.Throws<DomainException>(() => @case.MarkSent("WebPage", "summary", "key-1", Now));
    }

    [Fact]
    public void MarkSent_WithNoRoute_IsRefused()
    {
        Assert.Throws<DomainException>(() => Open().MarkSent("WebPage", "summary", "key-1", Now));
    }

    [Fact]
    public void MarkSent_RetriedWithTheSameKey_RecordsOneMessage()
    {
        var @case = Sent();

        var again = @case.MarkSent("WebPage", "Brand, model, serial, purchase date, issue", "key-1", Now);

        Assert.Equal((1, CaseStatus.Sent), (@case.Messages.Count, @case.Status));
        Assert.Same(@case.Messages[0], again);
    }

    [Fact]
    public void AddReference_TheSameReferenceTwice_IsKeptOnce()
    {
        var @case = Sent();

        @case.AddReference("Bosch", "4471-22", Now);
        @case.AddReference("Bosch", "4471-22", Now);

        Assert.Single(@case.References);
    }

    [Fact]
    public void RecordProviderUpdate_BeforeSending_IsRefused()
    {
        var (@case, _) = Routed();

        Assert.Throws<DomainException>(() => @case.RecordProviderUpdate(CaseStatus.Assessing, null, Now));
    }

    [Fact]
    public void RecordProviderUpdate_ToAClosedStatus_IsRefused()
    {
        Assert.Throws<DomainException>(() => Sent().RecordProviderUpdate(CaseStatus.Resolved, null, Now));
    }

    [Fact]
    public void RecordProviderUpdate_WithANote_CapturesAnInboundMessage()
    {
        var @case = Sent();

        @case.RecordProviderUpdate(CaseStatus.WaitingPart, "Part ordered, 10 days.", Now);

        Assert.Equal((CaseStatus.WaitingPart, MessageDirection.Inbound), (@case.Status, @case.Messages[^1].Direction));
    }

    [Fact]
    public void AddCommitment_SetsTheNextActionToTheEarliestOpenPromise()
    {
        var @case = Sent();

        @case.AddCommitment(CommitmentParty.Provider, "Call back", new DateOnly(2026, 11, 3), Now);
        @case.AddCommitment(CommitmentParty.Provider, "Quote", new DateOnly(2026, 10, 29), Now);

        Assert.Equal(new DateOnly(2026, 10, 29), @case.NextActionOn);
    }

    [Fact]
    public void AddCommitment_InThePast_IsRefused()
    {
        Assert.Throws<DomainException>(() => Sent().AddCommitment(CommitmentParty.Provider, "Call", new DateOnly(2026, 10, 1), Now));
    }

    [Fact]
    public void Commitment_OpenPastItsDate_IsOverdue()
    {
        var commitment = Sent().AddCommitment(CommitmentParty.Provider, "Call back", new DateOnly(2026, 10, 27), Now);

        Assert.Equal((false, true), (commitment.IsOverdueOn(new DateOnly(2026, 10, 27)), commitment.IsOverdueOn(new DateOnly(2026, 10, 28))));
    }

    [Fact]
    public void KeepCommitment_ClearsTheNextAction()
    {
        var @case = Sent();
        var commitment = @case.AddCommitment(CommitmentParty.Provider, "Call back", new DateOnly(2026, 10, 27), Now);

        @case.KeepCommitment(commitment.Id, Now);

        Assert.Null(@case.NextActionOn);
    }

    [Fact]
    public void BookAppointment_MovesTheCaseToAppointmentBooked()
    {
        var @case = Sent();

        @case.BookAppointment(Now.AddDays(3), Now.AddDays(3).AddHours(2), "Home", null, Now);

        Assert.Equal((CaseStatus.AppointmentBooked, DateOnly.FromDateTime(Now.AddDays(3).UtcDateTime)), (@case.Status, @case.NextActionOn));
    }

    [Fact]
    public void CompleteAppointment_MovesTheCaseToAssessing()
    {
        var @case = Sent();
        var appointment = @case.BookAppointment(Now.AddDays(3), null, null, null, Now);

        @case.CompleteAppointment(appointment.Id, Now.AddDays(3));

        Assert.Equal(CaseStatus.Assessing, @case.Status);
    }

    [Fact]
    public void Resolve_ClosesAndCancelsOpenPromisesAndRaisesClosed()
    {
        var @case = Sent();
        @case.AddCommitment(CommitmentParty.Provider, "Call back", new DateOnly(2026, 10, 27), Now);

        @case.Resolve(CaseOutcome.Repaired, "Motor replaced", Now);

        Assert.Equal((CaseStatus.Resolved, CommitmentStatus.Cancelled, (DateOnly?)null),
            (@case.Status, @case.Commitments[0].Status, @case.NextActionOn));
        Assert.Contains(@case.DomainEvents, e => e is AssetCaseClosedDomainEvent { Outcome: CaseOutcome.Repaired });
    }

    [Fact]
    public void Resolve_Rejected_ClosesAsRejected()
    {
        var @case = Sent();

        @case.Resolve(CaseOutcome.Rejected, null, Now);

        Assert.Equal(CaseStatus.Rejected, @case.Status);
    }

    [Fact]
    public void AClosedCase_TakesNoMoreChanges()
    {
        var @case = Sent();
        @case.Withdraw(Now);

        Assert.Throws<DomainException>(() => @case.AddReference("Bosch", "1", Now));
    }

    [Fact]
    public void ApplyRoute_AfterSending_IsRefused()
    {
        var @case = Sent();
        var decision = RoutingDecision.Override(Asset, RouteType.CarryIn, "Called", "{}", Now);

        Assert.Throws<DomainException>(() => @case.ApplyRoute(decision, null, Now));
    }

    [Fact]
    public void ApplyManualRoute_SetsThePersonsCounterpartyForTheOwnerToConfirm()
    {
        var @case = Open();
        var decision = RoutingDecision.Override(Asset, RouteType.CarryIn, "Called Bosch ZA", "{}", Now);

        @case.ApplyManualRoute(decision, "Bosch Service Centre Midrand", ContactChannelType.Phone, "0861 267 246", "Take it in.", Now);

        Assert.Equal((RouteType.CarryIn, false, "Bosch Service Centre Midrand"), (@case.RouteType, @case.RouteAccepted, @case.TargetName));
    }

    [Fact]
    public void Redact_ForgetsWhatTheOwnerWroteAndKeepsTheOutcome()
    {
        var @case = Sent();
        @case.RecordProviderUpdate(CaseStatus.Assessing, "Called me on 082 555 0101", Now);
        @case.Resolve(CaseOutcome.Repaired, "Fine", Now);

        @case.Redact();

        Assert.Equal((AssetCase.Redacted, AssetCase.Redacted, CaseOutcome.Repaired),
            (@case.IssueSummary, @case.Messages[^1].Body, @case.Outcome));
    }

    [Fact]
    public void Open_WithAnyAsTheIssueClass_IsRefused()
    {
        Assert.Throws<DomainException>(() => AssetCase.Open(Asset, IssueClass.Any, "Broken", false, CoverageState.Active, Now));
    }
}
