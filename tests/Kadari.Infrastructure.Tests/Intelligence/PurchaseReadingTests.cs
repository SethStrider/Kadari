using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Features.Assets.ReadReceipt;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Intelligence.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Intelligence;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Tests.Care;

namespace Kadari.Infrastructure.Tests.Intelligence;

/// <summary>
/// Reading a receipt with AI (CARE/14 C3) against real Postgres, with a fake provider: the proposal comes back, nothing
/// but the usage row is saved, the usage carries tokens and cost, and no customer data goes off-shore without the
/// cross-border consent.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PurchaseReadingTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private sealed class FakeGateway(bool crossBorder = true) : IModelGateway
    {
        public int Calls { get; private set; }
        public bool IsConfigured => true;
        public bool IsCrossBorder => crossBorder;

        public Task<ModelReply> CompleteAsync(ModelCall call, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ModelReply(
                true,
                """{"retailer":{"value":"Makro","confidence":0.9},"purchaseDate":{"value":"2026-03-14","confidence":0.85},"brand":{"value":"Bosch","confidence":0.8}}""",
                null,
                new ModelCallUsage("fake", "fake-vision", 1000, 200)));
        }
    }

    private sealed class ReceiptBlobs : IBlobStorage
    {
        public Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> DownloadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0]));   // a JPEG
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(true);
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    private ISender Sender(IKadariDbContext db, IModelGateway gateway)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AsStaff();
        services.AddSingleton(db);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IBlobStorage>(new ReceiptBlobs());
        services.AddSingleton(gateway);
        services.AddSingleton(TimeProvider.System);
        services.Configure<ModelPricingOptions>(o =>
            o.Models["fake-vision"] = new ModelPrice { InputPerMillionTokens = 0.10m, OutputPerMillionTokens = 0.40m });
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<(Guid Customer, Guid Asset)> SeedAsync(string mobile, bool crossBorderConsent)
    {
        var (customer, asset) = await CareTestHost.SeedAssetAsync(ConnectionString, mobile);
        await CareTestHost.AttachReceiptAsync(ConnectionString, asset);
        if (crossBorderConsent)
        {
            await using var db = CareTestHost.NewContext(ConnectionString);
            db.KadariConsents.Add(KadariConsent.Grant(new CustomerId(customer), KadariConsentScope.CrossBorderAiEnrichment, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        return (customer, asset);
    }

    [Fact]
    public async Task ReadReceipt_ProposesTheDetailsAndRecordsOnlyTheUsageWithItsCost()
    {
        var (customer, asset) = await SeedAsync("+27820002001", crossBorderConsent: true);

        PurchaseProposalDto proposal;
        await using (var db = CareTestHost.NewContext(ConnectionString))
        {
            proposal = await Sender(db, new FakeGateway()).Send(new ReadReceiptCommand(customer, asset));
        }

        await using var verify = CareTestHost.NewContext(ConnectionString);
        var usage = await verify.Set<ModelUsage>().SingleAsync();
        var purchaseEvidence = await verify.Assets.SelectMany(a => a.Evidence).CountAsync(e => e.Kind == AssetEvidenceKind.Purchase);
        Assert.Equal(
            ("Makro", "care-receipt", 1000, 180L, 0),   // 1000 × 0.10 + 200 × 0.40 = 180 micro-dollars
            (proposal.Retailer!.Value, usage.Feature, usage.InputTokens, usage.CostMicros, purchaseEvidence));
    }

    [Fact]
    public async Task ReadReceipt_WithoutCrossBorderConsent_SendsNothingOffShore()
    {
        var (customer, asset) = await SeedAsync("+27820002002", crossBorderConsent: false);
        var gateway = new FakeGateway(crossBorder: true);

        PurchaseProposalDto proposal;
        await using (var db = CareTestHost.NewContext(ConnectionString))
        {
            proposal = await Sender(db, gateway).Send(new ReadReceiptCommand(customer, asset));
        }

        Assert.Equal((ExtractionUnavailable.NoCrossBorderConsent, 0), (proposal.Unavailable, gateway.Calls));
    }

    [Fact]
    public async Task ReadReceipt_WithNoStoredReceipt_SaysSoWithoutCallingTheModel()
    {
        var (customer, asset) = await CareTestHost.SeedAssetAsync(ConnectionString, "+27820002003");
        var gateway = new FakeGateway(crossBorder: false);

        PurchaseProposalDto proposal;
        await using (var db = CareTestHost.NewContext(ConnectionString))
        {
            proposal = await Sender(db, gateway).Send(new ReadReceiptCommand(customer, asset));
        }

        Assert.Equal((ExtractionUnavailable.NoReceipt, 0), (proposal.Unavailable, gateway.Calls));
    }
}
