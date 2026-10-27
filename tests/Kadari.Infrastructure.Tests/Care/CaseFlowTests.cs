using Microsoft.EntityFrameworkCore;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.GetAssetHistory;
using Kadari.Application.Features.Assets.RecordPurchaseDetails;
using Kadari.Application.Features.Care.AcceptCaseRoute;
using Kadari.Application.Features.Care.AddCaseCommitment;
using Kadari.Application.Features.Care.AddCaseReference;
using Kadari.Application.Features.Care.EnableCare;
using Kadari.Application.Features.Care.GetCareSummary;
using Kadari.Application.Features.Care.GetCases;
using Kadari.Application.Features.Care.MarkCaseSent;
using Kadari.Application.Features.Care.RecordProviderUpdate;
using Kadari.Application.Features.Care.ReportIssue;
using Kadari.Application.Features.Care.ResolveCase;
using Kadari.Application.Features.Care.Services;
using Kadari.Application.Features.Customers.EraseCustomer;
using Kadari.Domain.Abstractions;
using Kadari.Domain.Care;
using static Kadari.Infrastructure.Tests.Care.CareTestHost;

namespace Kadari.Infrastructure.Tests.Care;

/// <summary>
/// "Something's wrong" end to end against real Postgres (CARE/14 C7–C11, CARE/15 G1 slices A–B): the case opens on the
/// existing asset, the route comes from the registry or goes to a person, the owner sends the pack once, the provider's
/// promise is chased, and the outcome lands in the asset's history.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CaseFlowTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private static readonly DateOnly Purchased = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

    private async Task<T> SendAsync<T>(MediatR.IRequest<T> request)
    {
        await using var db = NewContext(ConnectionString);
        return await Sender(db).Send(request);
    }

    /// <summary>A Hisense kettle bought at Makro, with a receipt, under a published 12-month warranty; the routing rule
    /// sends an in-cover fault back to Makro.</summary>
    private async Task<(Guid Customer, Guid Asset)> ArrangeAsync(string mobile, bool withRoute = true, string routeConfidence = "High")
    {
        var coverage = BoschRule(key: "hisense-small") with { Brand = "Hisense", Category = "SmallAppliance", DurationMonths = "12" };
        await ImportAsync(ConnectionString, Snapshot(coverage) with { RoutingRules = withRoute ? [HisenseRoute() with { Confidence = routeConfidence }] : [] });
        var (customer, asset) = await SeedAssetAsync(ConnectionString, mobile);
        await AttachReceiptAsync(ConnectionString, asset);
        await SendAsync(new RecordPurchaseDetailsCommand(customer, asset, "Hisense", "KT-17", "Makro", Purchased));
        await SendAsync(new EnableCareCommand(customer, asset, "SmallAppliance"));
        return (customer, asset);
    }

    private static ReportIssueCommand Report(Guid customer, Guid asset, string description = "It stopped heating.") =>
        new(customer, asset, "Fault", description, OwnerSaysUnsafe: false);

    private static MarkCaseSentCommand Send(Guid customer, Guid asset, Guid @case, string key = "send-1") =>
        new(customer, asset, @case, CasePack.TermsVersion, ExplicitConsent: true, key);

    [Fact]
    public async Task Report_WithAVerifiedRoute_OpensAGreenWarrantyCaseOnTheExistingAsset()
    {
        var (customer, asset) = await ArrangeAsync("+27820004001");

        var result = await SendAsync(Report(customer, asset));

        await using var db = NewContext(ConnectionString);
        Assert.Equal(
            ("RetailerCredit", "Green", true, "Warranty", 1, 1),
            (result.Route.RouteType, result.Route.Confidence, result.Route.Accepted, result.CaseType,
             await db.Assets.CountAsync(), await db.Set<RoutingDecision>().CountAsync()));
    }

    [Fact]
    public async Task Report_WithNoRoutingRule_GoesToAPersonAndOffersNothingToSend()
    {
        var (customer, asset) = await ArrangeAsync("+27820004002", withRoute: false);

        var result = await SendAsync(Report(customer, asset));

        Assert.Equal(("ResearchRequired", "Red", true, (CasePackDto?)null),
            (result.Route.RouteType, result.Route.Confidence, result.Route.NeedsRoute, result.Pack));
    }

    [Fact]
    public async Task Report_WithASafetySignal_ShowsStopUseGuidanceFirst()
    {
        var (customer, asset) = await ArrangeAsync("+27820004003");

        var result = await SendAsync(Report(customer, asset, "There is smoke coming out of the base."));

        Assert.Equal((true, SafetyGate.Guidance, "SafetyStop"), (result.SafetyFlagged, result.SafetyGuidance, result.Timeline[1].Type));
    }

    [Fact]
    public async Task Report_WithoutCare_IsAConflict()
    {
        var (customer, asset) = await SeedAssetAsync(ConnectionString, "+27820004004");

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(Report(customer, asset)));
    }

    [Fact]
    public async Task Report_CountsTheOpenCaseOnTheCareCard()
    {
        var (customer, asset) = await ArrangeAsync("+27820004005");

        await SendAsync(Report(customer, asset));

        Assert.Equal(1, (await SendAsync(new GetCareSummaryQuery(customer, asset))).OpenCaseCount);
    }

    [Fact]
    public async Task ThePack_SharesOnlyProductPurchaseAndProblem_NeverContactDetails()
    {
        var (customer, asset) = await ArrangeAsync("+27820004006");

        var result = await SendAsync(Report(customer, asset));

        Assert.Equal("Brand,Model,Issue,PurchaseDate,Retailer,ProofOfPurchase", string.Join(",", result.Pack!.Shared.Select(s => s.Field)));
    }

    [Fact]
    public async Task MarkSent_Replayed_RecordsOneHandoffAuthorisationAndAudit()
    {
        var (customer, asset) = await ArrangeAsync("+27820004007");
        var opened = await SendAsync(Report(customer, asset));

        await SendAsync(Send(customer, asset, opened.CaseId));
        var replay = await SendAsync(Send(customer, asset, opened.CaseId));

        await using var db = NewContext(ConnectionString);
        Assert.Equal(
            ("Sent", 1, 1, 1),
            (replay.Status, replay.Messages.Count,
             await db.Set<ExternalActionAuthorisation>().CountAsync(a => a.ActionType == ExternalActionType.CaseHandoff),
             await db.Set<ExternalActionAudit>().CountAsync(a => a.CaseId == opened.CaseId)));
    }

    [Fact]
    public async Task MarkSent_WithOldWording_IsRefused()
    {
        var (customer, asset) = await ArrangeAsync("+27820004008");
        var opened = await SendAsync(Report(customer, asset));

        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(Send(customer, asset, opened.CaseId) with { AcceptedTermsVersion = "care-case-handoff-v0" }));
    }

    [Fact]
    public async Task AProviderPromise_SchedulesAnOverdueChaseTheDayAfter()
    {
        var (customer, asset) = await ArrangeAsync("+27820004009");
        var opened = await SendAsync(Report(customer, asset));
        await SendAsync(Send(customer, asset, opened.CaseId));
        var due = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        await SendAsync(new AddCaseCommitmentCommand(customer, asset, opened.CaseId, "Provider", "Call back with a decision", due));

        await using var db = NewContext(ConnectionString);
        var reminder = await db.Set<CareReminder>().SingleAsync(r => r.Type == CareReminderType.CommitmentOverdue);
        Assert.Equal((due.AddDays(1), CareReminderStatus.Scheduled), (reminder.DueOn, reminder.Status));
    }

    [Fact]
    public async Task Resolve_WritesTheOutcomeToHistory_CancelsTheChase_AndClearsTheOpenCount()
    {
        var (customer, asset) = await ArrangeAsync("+27820004010");
        var opened = await SendAsync(Report(customer, asset));
        await SendAsync(Send(customer, asset, opened.CaseId));
        await SendAsync(new AddCaseReferenceCommand(customer, asset, opened.CaseId, "Makro", "RMA-55120"));
        await SendAsync(new RecordProviderUpdateCommand(customer, asset, opened.CaseId, "Assessing", null));
        await SendAsync(new AddCaseCommitmentCommand(
            customer, asset, opened.CaseId, "Provider", "Swap decision", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))));

        await SendAsync(new ResolveCaseCommand(customer, asset, opened.CaseId, "Replaced", "Swapped in store"));

        var history = await SendAsync(new GetAssetHistoryQuery(customer, asset));
        await using var db = NewContext(ConnectionString);
        Assert.Contains(history.Entries, e => e.Kind == "CaseResolved" && e.Summary == "A problem was resolved — replaced.");
        Assert.Equal(
            (CareReminderStatus.Cancelled, 0),
            ((await db.Set<CareReminder>().SingleAsync(r => r.Type == CareReminderType.CommitmentOverdue)).Status,
             (await SendAsync(new GetCareSummaryQuery(customer, asset))).OpenCaseCount));
    }

    [Fact]
    public async Task AnAmberRoute_MustBeAcceptedBeforeSending()
    {
        var (customer, asset) = await ArrangeAsync("+27820004011", routeConfidence: "Medium");
        var opened = await SendAsync(Report(customer, asset));

        await Assert.ThrowsAsync<DomainException>(() => SendAsync(Send(customer, asset, opened.CaseId)));
        await SendAsync(new AcceptCaseRouteCommand(customer, asset, opened.CaseId));
        var sent = await SendAsync(Send(customer, asset, opened.CaseId));

        Assert.Equal(("Amber", "Sent"), (opened.Route.Confidence, sent.Status));
    }

    [Fact]
    public async Task Cases_ListOpenOnesFirst()
    {
        var (customer, asset) = await ArrangeAsync("+27820004012");
        var first = await SendAsync(Report(customer, asset, "First problem"));
        await SendAsync(new ResolveCaseCommand(customer, asset, first.CaseId, "NoFaultFound", null));
        await SendAsync(Report(customer, asset, "Second problem"));

        var cases = await SendAsync(new GetCasesQuery(customer, asset));

        Assert.Equal(["Second problem", "First problem"], cases.Select(c => c.Title));
    }

    [Fact]
    public async Task Erasure_ForgetsWhatTheOwnerWroteOnTheirCases()
    {
        var (customer, asset) = await ArrangeAsync("+27820004013");
        var opened = await SendAsync(Report(customer, asset, "My number is 082 555 0101, call me"));

        await SendAsync(new EraseCustomerCommand(customer));

        await using var db = NewContext(ConnectionString);
        var stored = await db.Set<AssetCase>().IgnoreQueryFilters().SingleAsync(c => c.Id == new AssetCaseId(opened.CaseId));
        Assert.Equal((AssetCase.Redacted, AssetCase.Redacted), (stored.IssueSummary, stored.Title));
    }

}
