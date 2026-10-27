using Microsoft.EntityFrameworkCore;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.GetAssetHistory;
using Kadari.Application.Features.Assets.RecordPurchaseDetails;
using Kadari.Application.Features.Care.EnableCare;
using Kadari.Application.Features.Care.EvaluateCare;
using Kadari.Application.Features.Care.GetCareSummary;
using Kadari.Application.Features.Care.GetCoverage;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using static Kadari.Infrastructure.Tests.Care.CareTestHost;

namespace Kadari.Infrastructure.Tests.Care;

/// <summary>
/// KADARI Care against real Postgres (CARE/14 C1 + C4, CARE/15 G0): Care attaches to the existing asset without
/// creating another, survives reload, is isolated per owner, and every coverage answer is a recorded decision that
/// keeps pointing at the exact rule version it used — even after the rule changes.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CareFlowTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private static readonly DateOnly Purchased = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-7));

    private async Task<T> SendAsync<T>(MediatR.IRequest<T> request)
    {
        await using var db = NewContext(ConnectionString);
        return await Sender(db).Send(request);
    }

    private Task RecordPurchaseAsync(Guid customerId, Guid assetId, string brand = "Bosch Home", DateOnly? purchased = null) =>
        SendAsync(new RecordPurchaseDetailsCommand(customerId, assetId, brand, "WGG244", "Makro", purchased ?? Purchased));

    [Fact]
    public async Task EnableCare_AttachesOneProfileToTheExistingAssetAndCreatesNoOther()
    {
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001001");

        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));
        var again = await SendAsync(new EnableCareCommand(customerId, assetId, "majorappliance"));

        await using var verify = NewContext(ConnectionString);
        Assert.Equal(
            (1, 1, false, assetId),
            (await verify.Assets.CountAsync(), await verify.CareProfiles.CountAsync(), again.Created,
             (await verify.CareProfiles.SingleAsync()).AssetId.Value));
    }

    [Fact]
    public async Task EnableCare_OnAnotherCustomersAsset_IsNotFound()
    {
        var (_, assetOfA) = await SeedAssetAsync(ConnectionString, "+27820001002");
        var (customerB, _) = await SeedAssetAsync(ConnectionString, "+27820001003");

        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new EnableCareCommand(customerB, assetOfA, "MajorAppliance")));
    }

    [Fact]
    public async Task EnableCare_WithAnUnknownCategory_IsRejected()
    {
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001004");

        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new EnableCareCommand(customerId, assetId, "1")));
    }

    [Fact]
    public async Task Coverage_WithReceiptAndAPublishedRule_IsActiveAndCitesTheRuleVersion()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule()));
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001005");
        await AttachReceiptAsync(ConnectionString, assetId);
        await RecordPurchaseAsync(customerId, assetId);

        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));

        var coverage = await SendAsync(new GetCoverageQuery(customerId, assetId));
        var record = coverage.Records.Single();
        Assert.Equal(
            ("Active", Purchased.AddMonths(24), 1, "Bosch ZA warranty terms"),
            (record.State, record.ExpiresOn, record.HowKadariKnows!.RuleVersion, record.HowKadariKnows.SourceTitle));
    }

    [Fact]
    public async Task Coverage_WithoutAnyPublishedRule_IsUnknownWithNoDates()
    {
        await ImportAsync(ConnectionString, Snapshot());
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001006");
        await AttachReceiptAsync(ConnectionString, assetId);
        await RecordPurchaseAsync(customerId, assetId);

        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));

        var summary = await SendAsync(new GetCareSummaryQuery(customerId, assetId));
        Assert.Equal(("Unknown", "Unknown", (DateOnly?)null), (summary.CardState, summary.CoverageState, summary.CoverageExpiresOn));
    }

    [Fact]
    public async Task Coverage_WithTypedDetailsButNoReceipt_IsOnlyLikely()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule()));
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001007");
        await RecordPurchaseAsync(customerId, assetId);

        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));

        var summary = await SendAsync(new GetCareSummaryQuery(customerId, assetId));
        Assert.Equal(("LikelyActive", "AddProofOfPurchase"), (summary.CoverageState, summary.PrimaryAction));
    }

    [Fact]
    public async Task APublishedRuleChange_LeavesEarlierDecisionsOnTheVersionTheyUsed()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule(months: "24")));
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001008");
        await AttachReceiptAsync(ConnectionString, assetId);
        await RecordPurchaseAsync(customerId, assetId);
        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));

        await ImportAsync(ConnectionString, Snapshot(BoschRule(months: "36")));
        var second = await SendAsync(new EvaluateCareCommand(customerId, assetId));

        await using var verify = NewContext(ConnectionString);
        var versions = await verify.Set<CoverageRuleVersion>().OrderBy(v => v.Version).ToListAsync();
        var decisions = await verify.Set<CoverageDecision>().Where(d => d.AssetId == new AssetId(assetId))
            .OrderBy(d => d.EvaluatedAt).ToListAsync();
        Assert.Equal(
            (RuleStatus.Superseded, RuleStatus.Published, versions[0].Id.Value, versions[1].Id.Value),
            (versions[0].Status, versions[1].Status, decisions.First().MatchedRuleVersionIds.Single(),
             decisions.Single(d => d.Id.Value == second.CoverageDecisionId).MatchedRuleVersionIds.Single()));
    }

    [Fact]
    public async Task NewPurchaseDetails_ReevaluateCareInTheSameTransaction()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule()));
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001009");
        await AttachReceiptAsync(ConnectionString, assetId);
        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));

        await RecordPurchaseAsync(customerId, assetId);

        var summary = await SendAsync(new GetCareSummaryQuery(customerId, assetId));
        Assert.Equal("Active", summary.CoverageState);
    }

    [Fact]
    public async Task History_RecordsCareAndTheConfirmedWarrantyAgainstTheAsset()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule()));
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001010");
        await AttachReceiptAsync(ConnectionString, assetId);
        await RecordPurchaseAsync(customerId, assetId);

        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));
        await SendAsync(new EvaluateCareCommand(customerId, assetId));   // a re-check that finds the same writes nothing

        var history = await SendAsync(new GetAssetHistoryQuery(customerId, assetId));
        Assert.Equal(
            ["CoverageFound", "CareEnabled"],
            history.Entries.Select(e => e.Kind).Where(k => k is "CoverageFound" or "CareEnabled" or "CoverageChanged"));
    }

    [Fact]
    public async Task Summary_ForClothing_DegradesToNotCareRelevant()
    {
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001011", AssetCategory.Clothing);

        var summary = await SendAsync(new GetCareSummaryQuery(customerId, assetId));

        Assert.Equal(("NotCareRelevant", false), (summary.CardState, summary.Enabled));
    }

    [Fact]
    public async Task Summary_SurvivesReloadFromTheDatabase()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule()));
        var (customerId, assetId) = await SeedAssetAsync(ConnectionString, "+27820001012");
        await AttachReceiptAsync(ConnectionString, assetId);
        await RecordPurchaseAsync(customerId, assetId);
        await SendAsync(new EnableCareCommand(customerId, assetId, "MajorAppliance"));

        var summary = await SendAsync(new GetCareSummaryQuery(customerId, assetId));

        Assert.Equal(
            ("Active", "MajorAppliance", Purchased.AddMonths(24).AddDays(-90), 0),
            (summary.CardState, summary.Category, summary.NextAttentionOn, summary.OpenCaseCount));
    }
}
