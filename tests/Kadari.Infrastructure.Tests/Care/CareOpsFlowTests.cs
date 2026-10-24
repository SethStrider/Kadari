using Microsoft.EntityFrameworkCore;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.RecordPurchaseDetails;
using Kadari.Application.Features.Care.AcceptCaseRoute;
using Kadari.Application.Features.Care.AddCaseCommitment;
using Kadari.Application.Features.Care.DispatchDueCareReminders;
using Kadari.Application.Features.Care.EnableCare;
using Kadari.Application.Features.Care.GetCase;
using Kadari.Application.Features.Care.MarkCaseSent;
using Kadari.Application.Features.Care.ReportIssue;
using Kadari.Application.Features.Care.Services;
using Kadari.Application.Features.Care.WithdrawCase;
using Kadari.Application.Features.CareOps.GetOpsQueue;
using Kadari.Application.Features.CareOps.GetOpsWorkItem;
using Kadari.Application.Features.CareOps.ResolveOpsWorkItem;
using Kadari.Application.Features.CareOps.SetCaseRoute;
using Kadari.Domain.Care;
using static Kadari.Infrastructure.Tests.Care.CareTestHost;

namespace Kadari.Infrastructure.Tests.Care;

/// <summary>
/// Care Ops against real Postgres (CARE/14 C12): exceptions are queued once, an operator can resolve them, a route an
/// operator finds lets the customer's case resume, and the operator sees only what the task needs.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CareOpsFlowTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private async Task<T> SendAsync<T>(MediatR.IRequest<T> request)
    {
        await using var db = NewContext(ConnectionString);
        return await Sender(db).Send(request);
    }

    /// <summary>A Hisense kettle with Care on and no routing rules: every report needs a person.</summary>
    private async Task<(Guid Customer, Guid Asset)> ArrangeAsync(string mobile)
    {
        await ImportAsync(ConnectionString, Snapshot());
        var (customer, asset) = await SeedAssetAsync(ConnectionString, mobile);
        await SendAsync(new RecordPurchaseDetailsCommand(customer, asset, "Hisense", "KT-17", "Makro", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))));
        await SendAsync(new EnableCareCommand(customer, asset, "SmallAppliance"));
        return (customer, asset);
    }

    private static ReportIssueCommand Report(Guid customer, Guid asset, string description = "It stopped heating.") =>
        new(customer, asset, "Fault", description, OwnerSaysUnsafe: false);

    private async Task<OpsWorkItemDtoProbe> SingleOpenAsync(string workType)
    {
        var queue = await SendAsync(new GetOpsQueueQuery(WorkType: workType));
        var item = Assert.Single(queue);
        return new OpsWorkItemDtoProbe(item.WorkItemId, item.Severity);
    }

    private sealed record OpsWorkItemDtoProbe(Guid Id, string Severity);

    private static SetCaseRouteCommand Route(Guid workItem) => new(
        workItem, "RetailerCredit", "Makro (the store you bought it from)", "InStore", null,
        "Take it back to the store with the receipt.", "ReusableKnowledge", "Hisense SA: small appliances go back to the retailer.");

    [Fact]
    public async Task AnUnknownRoute_IsQueuedOnceAsStandardWork()
    {
        var (customer, asset) = await ArrangeAsync("+27820005001");

        await SendAsync(Report(customer, asset));

        Assert.Equal("P2", (await SingleOpenAsync("RouteUnknown")).Severity);
    }

    [Fact]
    public async Task ASafetySignal_IsQueuedAsImmediate()
    {
        var (customer, asset) = await ArrangeAsync("+27820005002");

        await SendAsync(Report(customer, asset, "It sparks when I switch it on"));

        Assert.Equal("P0", (await SingleOpenAsync("SafetyReview")).Severity);
    }

    [Fact]
    public async Task TheOperator_SeesTheProductButNotTheOwnersWords_ForARouteTask()
    {
        var (customer, asset) = await ArrangeAsync("+27820005003");
        await SendAsync(Report(customer, asset, "Call me on 082 555 0101"));
        var item = await SingleOpenAsync("RouteUnknown");

        var detail = await SendAsync(new GetOpsWorkItemQuery(item.Id));

        Assert.Equal(("Hisense", "KT-17", "SmallAppliance", (string?)null),
            (detail.Product!.Brand, detail.Product.Model, detail.Product.Category, detail.Case!.IssueSummary));
    }

    [Fact]
    public async Task ARouteFoundByOps_LetsTheCustomerConfirmAndSend()
    {
        var (customer, asset) = await ArrangeAsync("+27820005004");
        var opened = await SendAsync(Report(customer, asset));
        var item = await SingleOpenAsync("RouteUnknown");

        await SendAsync(Route(item.Id));
        var routed = await SendAsync(new GetCaseQuery(customer, asset, opened.CaseId));
        await SendAsync(new AcceptCaseRouteCommand(customer, asset, opened.CaseId));
        var sent = await SendAsync(new MarkCaseSentCommand(customer, asset, opened.CaseId, CasePack.TermsVersion, true, "ops-send-1"));

        await using var db = NewContext(ConnectionString);
        Assert.Equal(("RetailerCredit", false, "Sent", true),
            (routed.Route.RouteType, routed.Route.Accepted, sent.Status, await db.Set<RoutingDecision>().AnyAsync(d => d.HumanOverride)));
        Assert.Empty(await SendAsync(new GetOpsQueueQuery()));
    }

    [Fact]
    public async Task SetRoute_OnACaseAlreadySent_IsAConflict()
    {
        var (customer, asset) = await ArrangeAsync("+27820005005");
        var opened = await SendAsync(Report(customer, asset));
        var item = await SingleOpenAsync("RouteUnknown");
        await SendAsync(Route(item.Id));
        await SendAsync(new AcceptCaseRouteCommand(customer, asset, opened.CaseId));
        await SendAsync(new MarkCaseSentCommand(customer, asset, opened.CaseId, CasePack.TermsVersion, true, "ops-send-2"));

        await using (var db = NewContext(ConnectionString))
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE kadari.\"OpsWorkItems\" SET \"Status\" = 'Open'");
        }

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(Route(item.Id)));
    }

    [Fact]
    public async Task Resolve_RecordsTheKindOfKnowledge()
    {
        var (customer, asset) = await ArrangeAsync("+27820005006");
        await SendAsync(Report(customer, asset));
        var item = await SingleOpenAsync("RouteUnknown");

        var resolved = await SendAsync(new ResolveOpsWorkItemCommand(item.Id, "PolicyReview", "Needs a decision on imported brands."));

        Assert.Equal(("Resolved", "PolicyReview"), (resolved.Status, resolved.Resolution));
    }

    [Fact]
    public async Task AWithdrawnCase_ClosesItsOpenWork()
    {
        var (customer, asset) = await ArrangeAsync("+27820005007");
        var opened = await SendAsync(Report(customer, asset));

        await SendAsync(new WithdrawCaseCommand(customer, asset, opened.CaseId));

        Assert.Empty(await SendAsync(new GetOpsQueueQuery()));
    }

    [Fact]
    public async Task AnOverdueProviderPromise_IsQueuedAsStalledByTheSweep()
    {
        var (customer, asset) = await ArrangeAsync("+27820005008");
        var opened = await SendAsync(Report(customer, asset));
        var item = await SingleOpenAsync("RouteUnknown");
        await SendAsync(Route(item.Id));
        await SendAsync(new AcceptCaseRouteCommand(customer, asset, opened.CaseId));
        await SendAsync(new MarkCaseSentCommand(customer, asset, opened.CaseId, CasePack.TermsVersion, true, "ops-send-3"));
        await SendAsync(new AddCaseCommitmentCommand(
            customer, asset, opened.CaseId, "Provider", "Call back", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))));
        await using (var db = NewContext(ConnectionString))
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE kadari.\"CareReminders\" SET \"DueOn\" = CURRENT_DATE WHERE \"Type\" = 'CommitmentOverdue'");
        }

        await SendAsync(new DispatchDueCareRemindersCommand());
        await SendAsync(new DispatchDueCareRemindersCommand());

        Assert.Equal("P1", (await SingleOpenAsync("CaseStalled")).Severity);
    }
}
