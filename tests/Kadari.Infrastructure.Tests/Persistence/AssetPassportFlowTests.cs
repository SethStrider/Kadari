using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.GetAsset;
using Kadari.Application.Features.GetAssetPassport;
using Kadari.Application.Features.MintAssetPassport;
using Kadari.Application.Features.VerifyAssetPassport;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the ledger read + Asset Passport slices against real Postgres: read one captured asset with its
/// evidence, mint its passport from that evidence, read the passport back, and confirm minting is idempotent
/// (one passport, one stable VerificationId) and that a foreign/absent asset is a 404.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AssetPassportFlowTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AddSingleton<IBlobStorage>(new NoopBlobStorage());
        services.AddSingleton<IVisionEnricher>(new CannedEnricher());
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<Guid> CaptureOneAssetAsync(Customer customer)
    {
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);

        try
        {
            await using var db = NewContext();
            var response = await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Living room"));
            return response.AssetIds.Single();
        }
        finally
        {
            await image.DisposeAsync();
        }
    }

    private async Task<Customer> SeedCustomerAsync(string mobile, string email)
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

        return customer;
    }

    [Fact]
    public async Task GetAsset_returns_the_asset_with_its_evidence()
    {
        var customer = await SeedCustomerAsync("+27820000021", "jane.getasset@example.com");
        var assetId = await CaptureOneAssetAsync(customer);

        await using var db = NewContext();
        var asset = await BuildSender(db).Send(new GetAssetQuery(customer.Id.Value, assetId));

        Assert.Equal(assetId, asset.AssetId);
        Assert.Equal("Samsung 65\" TV", asset.DisplayName);
        Assert.Equal("Living room", asset.RoomLabel);
        Assert.Equal("Identified", asset.Assurance);
        Assert.Contains(asset.Evidence, e => e.Kind == "Image");
        Assert.Contains(asset.Evidence, e => e.Kind == "Value");
        Assert.Equal(2_749_900, asset.BestValueMinor);
    }

    [Fact]
    public async Task Mint_then_get_returns_a_passport_snapshotting_the_asset()
    {
        var customer = await SeedCustomerAsync("+27820000022", "jane.mint@example.com");
        var assetId = await CaptureOneAssetAsync(customer);

        AssetPassportDto minted;
        await using (var db = NewContext())
        {
            minted = await BuildSender(db).Send(new MintAssetPassportCommand(customer.Id.Value, assetId));
        }

        Assert.StartsWith("AP-", minted.VerificationId);
        Assert.Equal(assetId, minted.AssetId);
        Assert.Equal("Samsung 65\" TV", minted.DisplayName);
        Assert.Equal("Identified", minted.Assurance);
        Assert.Equal(2_749_900, minted.ReplacementValueMinor);

        await using (var db = NewContext())
        {
            var read = await BuildSender(db).Send(new GetAssetPassportQuery(customer.Id.Value, assetId));
            Assert.Equal(minted.PassportId, read.PassportId);
            Assert.Equal(minted.VerificationId, read.VerificationId);
        }
    }

    [Fact]
    public async Task Mint_is_idempotent_per_asset()
    {
        var customer = await SeedCustomerAsync("+27820000023", "jane.idem@example.com");
        var assetId = await CaptureOneAssetAsync(customer);

        AssetPassportDto first;
        AssetPassportDto second;
        await using (var db = NewContext())
        {
            first = await BuildSender(db).Send(new MintAssetPassportCommand(customer.Id.Value, assetId));
        }
        await using (var db = NewContext())
        {
            second = await BuildSender(db).Send(new MintAssetPassportCommand(customer.Id.Value, assetId));
        }

        Assert.Equal(first.PassportId, second.PassportId);
        Assert.Equal(first.VerificationId, second.VerificationId);

        await using (var verify = NewContext())
        {
            var count = await verify.AssetPassports.CountAsync(p => p.AssetId == new AssetId(assetId));
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task GetAsset_for_an_asset_not_in_the_ledger_is_not_found()
    {
        var customer = await SeedCustomerAsync("+27820000024", "jane.404@example.com");

        await using var db = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(db).Send(new GetAssetQuery(customer.Id.Value, Guid.CreateVersion7())));
    }

    [Fact]
    public async Task A_minted_passport_verifies_publicly_by_its_id_and_an_unknown_id_is_not_found()
    {
        var customer = await SeedCustomerAsync("+27820000026", "jane.verify@example.com");
        var assetId = await CaptureOneAssetAsync(customer);
        string verificationId;
        await using (var db = NewContext())
        {
            verificationId = (await BuildSender(db)
                .Send(new MintAssetPassportCommand(customer.Id.Value, assetId))).VerificationId;
        }

        await using var context = NewContext();
        var verified = await BuildSender(context).Send(new VerifyAssetPassportQuery(verificationId));

        Assert.Equal((true, "Samsung 65\" TV", 2_749_900L), (verified.Valid, verified.DisplayName, verified.ReplacementValueMinor));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context).Send(new VerifyAssetPassportQuery("AP-DOESNOTEXIST")));
    }
}

file sealed class NoopBlobStorage : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class CannedEnricher : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.94, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, Money.Zar(2_749_900), 0),
        }));
}
