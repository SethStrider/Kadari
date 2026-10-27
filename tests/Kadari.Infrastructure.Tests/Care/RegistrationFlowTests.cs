using Microsoft.EntityFrameworkCore;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Assets.GetAssetHistory;
using Kadari.Application.Features.Assets.RecordPurchaseDetails;
using Kadari.Application.Features.Care.AuthoriseRegistration;
using Kadari.Application.Features.Care.CompleteRegistration;
using Kadari.Application.Features.Care.EnableCare;
using Kadari.Application.Features.Care.GetCareSummary;
using Kadari.Application.Features.Care.GetRegistration;
using Kadari.Application.Features.Care.Services;
using Kadari.Application.Features.Customers.EraseCustomer;
using Kadari.Domain.Care;
using static Kadari.Infrastructure.Tests.Care.CareTestHost;

namespace Kadari.Infrastructure.Tests.Care;

/// <summary>
/// Warranty registration end to end against real Postgres (CARE/14 C5, CARE/15 G1 slice C): a registration rule offers
/// extra cover; the owner sees exactly what would be shared and authorises; a replayed request starts nothing new;
/// confirmation with the provider's reference completes it, extends the cover and writes history; nothing is complete
/// without proof.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationFlowTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private static readonly DateOnly Purchased = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));

    private async Task<T> SendAsync<T>(MediatR.IRequest<T> request)
    {
        await using var db = NewContext(ConnectionString);
        return await Sender(db).Send(request);
    }

    /// <summary>A Bosch washer with a receipt and purchase details; the coverage rule needs registration and adds 12
    /// months once registered.</summary>
    private async Task<(Guid Customer, Guid Asset, OpportunityDto Offer)> ArrangeAsync(string mobile)
    {
        var coverage = BoschRule() with { RegistrationRequired = "yes", ExtensionMonths = "12" };
        await ImportAsync(ConnectionString, SnapshotWith([coverage], [BoschRegistration()]));
        var (customer, asset) = await SeedAssetAsync(ConnectionString, mobile);
        await AttachReceiptAsync(ConnectionString, asset);
        await SendAsync(new RecordPurchaseDetailsCommand(customer, asset, "Bosch Home", "WGG244", "Makro", Purchased));
        await SendAsync(new EnableCareCommand(customer, asset, "MajorAppliance"));
        var overview = await SendAsync(new GetRegistrationQuery(customer, asset));
        return (customer, asset, Assert.Single(overview.Opportunities));
    }

    private AuthoriseRegistrationCommand Authorise(Guid customer, Guid asset, Guid opportunity, string key = "key-1") =>
        new(customer, asset, opportunity, RegistrationPack.TermsVersion, ExplicitConsent: true, key);

    [Fact]
    public async Task TheOffer_ShowsTheDeadlineAndExactlyTheFieldsTheRuleNeeds()
    {
        var (_, _, offer) = await ArrangeAsync("+27820003001");

        Assert.Equal(
            (Purchased.AddDays(28), "Guided", "OwnerName,Email,SerialNumber,PurchaseDate"),
            (offer.DeadlineOn!.Value, offer.Mode, string.Join(",", offer.Shared.Select(s => s.Field))));
    }

    [Fact]
    public async Task BeforeRegistration_TheCoverIsOnlyLikely()
    {
        var (customer, asset, _) = await ArrangeAsync("+27820003002");

        var summary = await SendAsync(new GetCareSummaryQuery(customer, asset));

        Assert.Equal("LikelyActive", summary.CoverageState);
    }

    [Fact]
    public async Task Authorise_ReplayedWithTheSameKey_ReturnsTheSameRegistration()
    {
        var (customer, asset, offer) = await ArrangeAsync("+27820003003");

        var first = await SendAsync(Authorise(customer, asset, offer.OpportunityId));
        var replay = await SendAsync(Authorise(customer, asset, offer.OpportunityId));

        await using var verify = NewContext(ConnectionString);
        Assert.Equal(
            (first.RegistrationId, 1, 1),
            (replay.RegistrationId, await verify.Set<CoverageRegistration>().CountAsync(), await verify.Set<ExternalActionAuthorisation>().CountAsync()));
    }

    [Fact]
    public async Task Authorise_WithANewKeyAfterTheOfferWasTaken_IsRefused()
    {
        var (customer, asset, offer) = await ArrangeAsync("+27820003004");
        await SendAsync(Authorise(customer, asset, offer.OpportunityId));

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(Authorise(customer, asset, offer.OpportunityId, "key-2")));
    }

    [Fact]
    public async Task Authorise_AgainstOutdatedWording_IsRefused()
    {
        var (customer, asset, offer) = await ArrangeAsync("+27820003005");

        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(Authorise(customer, asset, offer.OpportunityId) with { AcceptedTermsVersion = "old" }));
    }

    [Fact]
    public async Task Authorise_WithoutExplicitConsent_IsRejected()
    {
        var (customer, asset, offer) = await ArrangeAsync("+27820003006");

        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(Authorise(customer, asset, offer.OpportunityId) with { ExplicitConsent = false }));
    }

    [Fact]
    public async Task Complete_WithTheProvidersReference_ExtendsTheCoverAndWritesHistory()
    {
        var (customer, asset, offer) = await ArrangeAsync("+27820003007");
        var registration = await SendAsync(Authorise(customer, asset, offer.OpportunityId));

        var completed = await SendAsync(new CompleteRegistrationCommand(
            customer, asset, registration.RegistrationId, "MB-889900", DateOnly.FromDateTime(DateTime.UtcNow)));

        var summary = await SendAsync(new GetCareSummaryQuery(customer, asset));
        var history = await SendAsync(new GetAssetHistoryQuery(customer, asset));
        Assert.Equal(
            ("Completed", "Active", Purchased.AddMonths(36), true),
            (completed.Status, summary.CoverageState, summary.CoverageExpiresOn!.Value,
             history.Entries.Any(e => e.Kind == "RegistrationCompleted" && e.Summary.Contains("MB-889900"))));
    }

    [Fact]
    public async Task EveryStep_IsAudited()
    {
        var (customer, asset, offer) = await ArrangeAsync("+27820003008");
        var registration = await SendAsync(Authorise(customer, asset, offer.OpportunityId));
        await SendAsync(new CompleteRegistrationCommand(customer, asset, registration.RegistrationId, "MB-1", DateOnly.FromDateTime(DateTime.UtcNow)));

        await using var verify = NewContext(ConnectionString);
        var steps = await verify.Set<ExternalActionAudit>().OrderBy(a => a.OccurredAt).Select(a => a.Step).ToListAsync();
        Assert.Equal(["Authorised", "Confirmed"], steps);
    }

    [Fact]
    public async Task Erasure_RedactsWhatTheAuthorityRecordedAsShared()
    {
        var (customer, asset, offer) = await ArrangeAsync("+27820003009");
        await SendAsync(Authorise(customer, asset, offer.OpportunityId));

        // The authority was granted by the staff test actor; give it to the customer's own account for the erasure.
        await using (var db = NewContext(ConnectionString))
        {
            var account = Kadari.Domain.Identity.UserAccount.Register("+27820003009", "Thandi", "Mokoena", "ZA", Kadari.Domain.Access.Role.Customer);
            account.LinkToCustomer(new Kadari.Domain.Customers.CustomerId(customer));
            db.UserAccounts.Add(account);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE kadari.\"ExternalActionAuthorisations\" SET \"GrantedByAccountId\" = {0}", account.Id.Value);
        }

        await SendAsync(new EraseCustomerCommand(customer));

        await using var verify = NewContext(ConnectionString);
        var authority = await verify.Set<ExternalActionAuthorisation>().SingleAsync();
        Assert.Equal((false, true), (authority.ScopeJson.Contains("Thandi"), authority.RevokedAt is not null));
    }
}
