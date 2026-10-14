using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AddHouseholdMember;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.GetAsset;
using Kadari.Application.Features.GetAssetPassport;
using Kadari.Application.Features.GetLedger;
using Kadari.Application.Features.MintAssetPassport;
using Kadari.Application.Features.RemoveHouseholdMember;
using Kadari.Application.Features.TransferAsset;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves multi-member household ownership against real Postgres: adding a co-owner makes a shared asset appear
/// in that member's ledger and detail (without duplicating it) while the primary owner keeps it, and removing
/// them takes it back out. Plus the 404 path.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class HouseholdMembershipTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AddSingleton<IBlobStorage>(new Silent2());
        services.AddSingleton<IVisionEnricher>(new Ident2());
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

    private async Task<Guid> CaptureAsync(Customer customer)
    {
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            return (await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.9, 0, 0, 1, 1)], "Lounge"))).AssetIds.Single();
        }
        finally
        {
            await image.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_co_owned_asset_appears_in_both_members_ledgers()
    {
        var owner = await SeedCustomerAsync("+27820000121", "owner@example.com");
        var partner = await SeedCustomerAsync("+27820000122", "partner@example.com");
        var assetId = await CaptureAsync(owner);

        // Before sharing: the partner sees nothing.
        await using (var db = NewContext())
        {
            Assert.Equal(0, (await BuildSender(db).Send(new GetLedgerQuery(partner.Id.Value))).AssetCount);
        }

        await using (var db = NewContext())
        {
            var response = await BuildSender(db).Send(new AddHouseholdMemberCommand(owner.Id.Value, assetId, partner.Id.Value));
            Assert.Equal(1, response.CoOwnerCount);
        }

        // After sharing: both see the single asset, and the partner can open it.
        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            Assert.Equal(1, (await sender.Send(new GetLedgerQuery(owner.Id.Value))).AssetCount);
            Assert.Equal(1, (await sender.Send(new GetLedgerQuery(partner.Id.Value))).AssetCount);

            var detail = await sender.Send(new GetAssetQuery(partner.Id.Value, assetId));
            Assert.Equal(assetId, detail.AssetId);
        }
    }

    [Fact]
    public async Task Removing_a_member_takes_the_asset_out_of_their_ledger()
    {
        var owner = await SeedCustomerAsync("+27820000123", "owner2@example.com");
        var partner = await SeedCustomerAsync("+27820000124", "partner2@example.com");
        var assetId = await CaptureAsync(owner);

        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new AddHouseholdMemberCommand(owner.Id.Value, assetId, partner.Id.Value));
        }
        await using (var db = NewContext())
        {
            var response = await BuildSender(db).Send(new RemoveHouseholdMemberCommand(owner.Id.Value, assetId, partner.Id.Value));
            Assert.Equal(0, response.CoOwnerCount);
        }

        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            Assert.Equal(0, (await sender.Send(new GetLedgerQuery(partner.Id.Value))).AssetCount);
            Assert.Equal(1, (await sender.Send(new GetLedgerQuery(owner.Id.Value))).AssetCount);   // owner keeps it
        }
    }

    [Fact]
    public async Task A_co_owner_can_read_the_shared_assets_passport()
    {
        var owner = await SeedCustomerAsync("+27820000126", "owner4@example.com");
        var partner = await SeedCustomerAsync("+27820000127", "partner4@example.com");
        var assetId = await CaptureAsync(owner);

        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            await sender.Send(new MintAssetPassportCommand(owner.Id.Value, assetId));           // owner mints
            await sender.Send(new AddHouseholdMemberCommand(owner.Id.Value, assetId, partner.Id.Value));
        }

        await using (var db = NewContext())
        {
            var passport = await BuildSender(db).Send(new GetAssetPassportQuery(partner.Id.Value, assetId));
            Assert.Equal(assetId, passport.AssetId);   // the co-owner reads it too
        }
    }

    [Fact]
    public async Task Transferring_the_asset_clears_the_household()
    {
        var owner = await SeedCustomerAsync("+27820000128", "owner5@example.com");
        var partner = await SeedCustomerAsync("+27820000129", "partner5@example.com");
        var buyer = await SeedCustomerAsync("+27820000130", "buyer5@example.com");
        var assetId = await CaptureAsync(owner);

        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new AddHouseholdMemberCommand(owner.Id.Value, assetId, partner.Id.Value));
        }

        // Owner sells the whole asset to the buyer.
        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new TransferAssetCommand(owner.Id.Value, assetId, buyer.Id.Value));
        }

        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            Assert.Equal(1, (await sender.Send(new GetLedgerQuery(buyer.Id.Value))).AssetCount);    // buyer has it
            Assert.Equal(0, (await sender.Send(new GetLedgerQuery(partner.Id.Value))).AssetCount);  // the old co-owner does not
        }

        await using (var verify = NewContext())
        {
            var asset = await verify.Assets.Include(a => a.CoOwners).SingleAsync(a => a.Id == new AssetId(assetId));
            Assert.Empty(asset.CoOwners);
        }
    }

    [Fact]
    public async Task Adding_an_unknown_member_is_not_found()
    {
        var owner = await SeedCustomerAsync("+27820000125", "owner3@example.com");
        var assetId = await CaptureAsync(owner);

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context).Send(new AddHouseholdMemberCommand(owner.Id.Value, assetId, Guid.CreateVersion7())));
    }
}

file sealed class Silent2 : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class Ident2 : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.9, 0, 0, 1, 1, "Samsung TV", AssetCategory.Electronics, "good", null, Money.Zar(2_000_00), 0),
        }));
}
