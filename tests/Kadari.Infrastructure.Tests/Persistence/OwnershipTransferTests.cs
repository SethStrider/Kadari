using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.GetLedger;
using Kadari.Application.Features.MintAssetPassport;
using Kadari.Application.Features.SettleEstate;
using Kadari.Application.Features.TransferAsset;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the household/estate ownership transitions against real Postgres: a transfer re-points an asset and
/// its passport to the recipient (leaving the giver's ledger, entering the recipient's), and estate settlement
/// passes a deceased customer's whole cohort to an heir. Plus the 404 paths.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OwnershipTransferTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AddSingleton<IBlobStorage>(new NoBlob());
        services.AddSingleton<IVisionEnricher>(new Ident());
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<Customer> SeedCustomerAsync(string mobile, string email)
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

        return customer;
    }

    private async Task<Guid> CaptureAsync(Customer customer, string room = "Lounge")
    {
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            var response = await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.9, 0, 0, 1, 1)], room));
            return response.AssetIds.Single();
        }
        finally
        {
            await image.DisposeAsync();
        }
    }

    [Fact]
    public async Task Transfer_moves_the_asset_and_its_passport_to_the_recipient()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var giver = await SeedCustomerAsync("+27820000111", "giver@example.com");
        var recipient = await SeedCustomerAsync("+27820000112", "recipient@example.com");
        var assetId = await CaptureAsync(giver);
        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new MintAssetPassportCommand(giver.Id.Value, assetId));
        }

        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new TransferAssetCommand(giver.Id.Value, assetId, recipient.Id.Value));
        }

        await using (var verify = NewContext())
        {
            var recipientSubject = await verify.Owners.Where(s => s.CustomerId == recipient.Id).Select(s => s.Id).SingleAsync();
            var asset = await verify.Assets.SingleAsync(a => a.Id == new AssetId(assetId));
            Assert.Equal(recipientSubject, asset.OwnerId);

            var passport = await verify.AssetPassports.SingleAsync(p => p.AssetId == new AssetId(assetId));
            Assert.Equal(recipientSubject, passport.OwnerId);
        }

        // It leaves the giver's ledger and enters the recipient's.
        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            Assert.Equal(0, (await sender.Send(new GetLedgerQuery(giver.Id.Value))).AssetCount);
            Assert.Equal(1, (await sender.Send(new GetLedgerQuery(recipient.Id.Value))).AssetCount);
        }
    }

    [Fact]
    public async Task Transferring_to_an_unknown_recipient_is_not_found()
    {
        var giver = await SeedCustomerAsync("+27820000113", "giver2@example.com");
        var assetId = await CaptureAsync(giver);

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context).Send(new TransferAssetCommand(giver.Id.Value, assetId, Guid.CreateVersion7())));
    }

    [Fact]
    public async Task Settling_an_estate_passes_the_whole_cohort_to_the_heir()
    {
        var deceased = await SeedCustomerAsync("+27820000114", "deceased@example.com");
        var heir = await SeedCustomerAsync("+27820000115", "heir@example.com");
        await CaptureAsync(deceased, "Lounge");
        await CaptureAsync(deceased, "Study");

        SettleEstateResponse response;
        await using (var db = NewContext())
        {
            response = await BuildSender(db).Send(new SettleEstateCommand(deceased.Id.Value, heir.Id.Value));
        }

        Assert.Equal(2, response.AssetsInherited);

        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            Assert.Equal(0, (await sender.Send(new GetLedgerQuery(deceased.Id.Value))).AssetCount);
            Assert.Equal(2, (await sender.Send(new GetLedgerQuery(heir.Id.Value))).AssetCount);
        }
    }

    [Fact]
    public async Task Settling_an_estate_for_an_unknown_heir_is_not_found()
    {
        var deceased = await SeedCustomerAsync("+27820000116", "deceased2@example.com");

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context).Send(new SettleEstateCommand(deceased.Id.Value, Guid.CreateVersion7())));
    }
}

file sealed class NoBlob : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class Ident : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.9, 0, 0, 1, 1, "Samsung TV", AssetCategory.Electronics, "good", null, Money.Zar(2_000_00), 0),
        }));
}
