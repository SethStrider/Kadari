using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.Exit;
using Kadari.Application.Features.GetAssetPassport;
using Kadari.Application.Features.MintAssetPassport;
using Kadari.Domain.Owners;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the exit-to-PoziSnap seam against real Postgres: a customer exports a signed asset-for-sale token
/// (requires a minted passport), anyone verifies it without a login, and verification fails once the passport
/// is revoked or when the token is tampered with.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AssetExportFlowTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IBlobStorage>(new Quiet());
        services.AddSingleton<IVisionEnricher>(new Identifies());
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<(Customer Customer, Guid AssetId)> SeedCapturedAndPassportedAsync(string mobile, string email)
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Jane", "Doe", mobile, "ZA", email: email);
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        Guid assetId;
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            var response = await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.9, 0, 0, 1, 1)], "Lounge"));
            assetId = response.AssetIds.Single();
        }
        finally
        {
            await image.DisposeAsync();
        }

        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new MintAssetPassportCommand(customer.Id.Value, assetId));
        }

        return (customer, assetId);
    }

    [Fact]
    public async Task Export_then_verify_confirms_provenance()
    {
        var (customer, assetId) = await SeedCapturedAndPassportedAsync("+27820000101", "jane.export@example.com");

        AssetForSaleExportDto export;
        await using (var db = NewContext())
        {
            export = await BuildSender(db).Send(new ExportAssetForSaleQuery(customer.Id.Value, assetId));
        }

        Assert.Equal(assetId, export.Package.AssetId);
        Assert.StartsWith("AP-", export.Package.PassportVerificationId);
        Assert.False(string.IsNullOrWhiteSpace(export.Token));

        await using (var db = NewContext())
        {
            var result = await BuildSender(db).Send(new VerifyAssetExportQuery(export.Token));
            Assert.True(result.Valid);
            Assert.Equal(assetId, result.Package!.AssetId);
        }
    }

    [Fact]
    public async Task A_tampered_token_does_not_verify()
    {
        var (customer, assetId) = await SeedCapturedAndPassportedAsync("+27820000102", "jane.tamper@example.com");

        AssetForSaleExportDto export;
        await using (var db = NewContext())
        {
            export = await BuildSender(db).Send(new ExportAssetForSaleQuery(customer.Id.Value, assetId));
        }

        var dot = export.Token.LastIndexOf('.');
        var tampered = (export.Token[0] == 'A' ? 'B' : 'A') + export.Token[1..];   // corrupt the payload

        await using (var db = NewContext())
        {
            var result = await BuildSender(db).Send(new VerifyAssetExportQuery(tampered));
            Assert.False(result.Valid);
        }
        Assert.True(dot > 0);
    }

    [Fact]
    public async Task A_revoked_passport_fails_verification_despite_a_genuine_signature()
    {
        var (customer, assetId) = await SeedCapturedAndPassportedAsync("+27820000103", "jane.revoke@example.com");

        AssetForSaleExportDto export;
        await using (var db = NewContext())
        {
            export = await BuildSender(db).Send(new ExportAssetForSaleQuery(customer.Id.Value, assetId));
        }

        // Revoke the passport directly (there is no revoke command yet).
        await using (var db = NewContext())
        {
            var passport = await db.AssetPassports.SingleAsync(p => p.AssetId == new AssetId(assetId));
            passport.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var result = await BuildSender(db).Send(new VerifyAssetExportQuery(export.Token));
            Assert.False(result.Valid);   // signature is genuine, but the credential is no longer live
        }
    }

    [Fact]
    public async Task Exporting_an_asset_without_a_passport_is_not_found()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Jane", "Doe", "+27820000104", "ZA", email: "jane.nopass@example.com");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        // Capture (creates the asset) but never mint a passport.
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        Guid assetId;
        try
        {
            await using var db = NewContext();
            assetId = (await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.9, 0, 0, 1, 1)], "Lounge"))).AssetIds.Single();
        }
        finally
        {
            await image.DisposeAsync();
        }

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context).Send(new ExportAssetForSaleQuery(customer.Id.Value, assetId)));
    }

    [Fact]
    public async Task A_household_co_owner_can_read_the_passport_but_cannot_export_the_shared_asset()
    {
        // Jane owns and passports an asset.
        var (jane, assetId) = await SeedCapturedAndPassportedAsync("+27820000105", "jane.coexport@example.com");

        // Bob is a second customer; capturing gives him a Owner (co-ownership is keyed by OwnerId).
        var bob = Customer.Register("Bob", "Roe", "+27820000106", "ZA", email: "bob.coexport@example.com");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(bob);
            await db.SaveChangesAsync();
        }

        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            await BuildSender(db).Send(new AnalyzeSnapCommand(
                bob.Id.Value, image, [new DetectedObject("chair", 0.9, 0, 0, 1, 1)], "Study"));
        }
        finally
        {
            await image.DisposeAsync();
        }

        // Share Jane's asset with Bob as a household co-owner.
        await using (var db = NewContext())
        {
            var bobSubjectId = await db.Owners
                .Where(s => s.CustomerId == bob.Id).Select(s => s.Id).SingleAsync();
            var asset = await db.Assets.SingleAsync(a => a.Id == new AssetId(assetId));
            asset.AddCoOwner(bobSubjectId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        // A co-owner may READ the shared credential...
        await using (var db = NewContext())
        {
            var passport = await BuildSender(db).Send(new GetAssetPassportQuery(bob.Id.Value, assetId));
            Assert.Equal(assetId, passport.AssetId);
        }

        // ...but may NOT export it for sale — disposal stays with the primary owner (a co-owner must not
        // unilaterally list a jointly-owned asset). Export is intentionally owner-only, so Bob gets a 404.
        await using (var db = NewContext())
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                BuildSender(db).Send(new ExportAssetForSaleQuery(bob.Id.Value, assetId)));
        }

        Assert.NotEqual(jane.Id, bob.Id);
    }
}

file sealed class Quiet : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class Identifies : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.9, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, Money.Zar(2_749_900), 0),
        }));
}
