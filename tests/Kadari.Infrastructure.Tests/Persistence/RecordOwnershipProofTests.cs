using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.RecordOwnershipProof;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the receipt/serial proof-of-ownership path against real Postgres: independent evidence lifts a
/// captured (Identified) asset up the assurance ladder — a serial to OwnershipSupported, a receipt with its
/// price to ValueSupported — and the pipeline rejects malformed proofs.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RecordOwnershipProofTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(KadariDbContext context, IBlobStorage blobs)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton(blobs);
        services.AddSingleton<IVisionEnricher>(new IdentifyingEnricher());
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<(Customer Customer, Guid AssetId)> SeedIdentifiedAssetAsync(string mobile, string email)
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Jane", "Doe", mobile, "ZA", email: email);
        await using (var seed = NewContext())
        {
            seed.AddCustomerConsentingToCapture(customer);
            await seed.SaveChangesAsync();
        }

        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            var response = await BuildSender(db, new ProofBlobStorage()).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Living room"));
            return (customer, response.AssetIds.Single());
        }
        finally
        {
            await image.DisposeAsync();
        }
    }

    private static ReceiptDocument ATempReceipt()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, "%PDF-1.7"u8.ToArray());   // a PDF header (GL-18 sniffs it)
        return new ReceiptDocument(path, "application/pdf", 8);
    }

    [Fact]
    public async Task A_serial_lifts_a_captured_asset_to_ownership_supported()
    {
        var (customer, assetId) = await SeedIdentifiedAssetAsync("+27820000031", "jane.serial@example.com");

        RecordOwnershipProofResponse response;
        await using (var db = NewContext())
        {
            response = await BuildSender(db, new ProofBlobStorage()).Send(new RecordOwnershipProofCommand(
                customer.Id.Value, assetId, Receipt: null, SerialNumber: "SN-12345", AmountMinor: null, Currency: null));
        }

        Assert.Equal(nameof(AssetAssuranceLevel.OwnershipSupported), response.Assurance);

        await using (var verify = NewContext())
        {
            var asset = await verify.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == new AssetId(assetId));
            Assert.Contains(asset.Evidence, e =>
                e.Kind == AssetEvidenceKind.Ownership && e.Strength == EvidenceStrength.Independent && e.Reference == "SN12345");
        }
    }

    [Fact]
    public async Task A_receipt_with_a_price_lifts_the_asset_to_value_supported()
    {
        var (customer, assetId) = await SeedIdentifiedAssetAsync("+27820000032", "jane.receipt@example.com");

        var blobs = new ProofBlobStorage();
        var receipt = ATempReceipt();
        RecordOwnershipProofResponse response;
        try
        {
            await using var db = NewContext();
            response = await BuildSender(db, blobs).Send(new RecordOwnershipProofCommand(
                customer.Id.Value, assetId, receipt, SerialNumber: null, AmountMinor: 2_499_00, Currency: "ZAR"));
        }
        finally
        {
            await receipt.DisposeAsync();
        }

        Assert.Equal(nameof(AssetAssuranceLevel.ValueSupported), response.Assurance);
        Assert.Single(blobs.Stored);

        await using (var verify = NewContext())
        {
            var asset = await verify.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == new AssetId(assetId));
            var value = asset.Evidence.Single(e => e.Kind == AssetEvidenceKind.Value);
            Assert.Equal(EvidenceStrength.Independent, value.Strength);
            Assert.Equal(Money.Zar(2_499_00), value.Amount);
            Assert.Contains(asset.Evidence, e => e.Kind == AssetEvidenceKind.Ownership && e.Strength == EvidenceStrength.Independent);
        }
    }

    [Fact]
    public async Task Proof_with_neither_receipt_nor_serial_is_rejected()
    {
        var (customer, assetId) = await SeedIdentifiedAssetAsync("+27820000033", "jane.empty@example.com");

        await using var db = NewContext();
        await Assert.ThrowsAsync<ValidationException>(() => BuildSender(db, new ProofBlobStorage()).Send(new RecordOwnershipProofCommand(
            customer.Id.Value, assetId, Receipt: null, SerialNumber: null, AmountMinor: null, Currency: null)));
    }

    [Fact]
    public async Task An_amount_without_a_receipt_is_rejected()
    {
        var (customer, assetId) = await SeedIdentifiedAssetAsync("+27820000034", "jane.noreceipt@example.com");

        await using var db = NewContext();
        await Assert.ThrowsAsync<ValidationException>(() => BuildSender(db, new ProofBlobStorage()).Send(new RecordOwnershipProofCommand(
            customer.Id.Value, assetId, Receipt: null, SerialNumber: "SN-9", AmountMinor: 1000, Currency: "ZAR")));
    }
}

file sealed class ProofBlobStorage : IBlobStorage
{
    public Dictionary<string, byte[]> Stored { get; } = [];

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        Stored[key] = ms.ToArray();
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(Stored[key]));

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Stored.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Stored.Remove(key);
        return Task.CompletedTask;
    }
}

file sealed class IdentifyingEnricher : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.94, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, null, 0),
        }));
}
