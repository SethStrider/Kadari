using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Features.Customers.EraseCustomer;
using Kadari.Application.Features.Customers.ExportCustomerData;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.DataLifecycle;

/// <summary>
/// Proves a customer's POPIA erasure and subject-access export cover their Kadari data (AS-03) against real
/// Postgres. The household is: an asset the customer owns alone, one they share with a partner, and one the
/// partner owns that the customer co-owns.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class KadariDataRightsTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private sealed class InMemoryBlobStorage : IBlobStorage
    {
        public HashSet<string> Keys { get; } = [];
        public Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            Keys.Add(key);
            return Task.CompletedTask;
        }
        public Task<Stream> DownloadAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(Keys.Contains(key));
        public Task DeleteAsync(string key, CancellationToken ct = default) { Keys.Remove(key); return Task.CompletedTask; }
    }

    private readonly InMemoryBlobStorage _blobs = new();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private ISender BuildSender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IBlobStorage>(_blobs);
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed record Household(
        Customer Customer, OwnerId Partner, Asset OwnedAlone, Asset Shared, Asset PartnersAsset,
        string PhotoKey, string ReceiptKey, string SharedPhotoKey);

    private async Task<Household> SeedHouseholdAsync()
    {
        await using (var migrate = NewContext())
        {
            await migrate.Database.MigrateAsync();
        }

        var customer = Customer.Register("Thandiwe", "Nkosi", $"+2782{Random.Shared.Next(1000000, 9999999)}", "ZA");
        var subject = Owner.ForCustomer(customer.Id, "Thandiwe Nkosi");
        var partner = Owner.ForPerson("Sipho Nkosi");
        var prefix = $"kadari/{customer.Id.Value:N}";

        var ownedAlone = Asset.Register(TenantId.Default, subject.Id, "MacBook Air", AssetCategory.Electronics, Now);
        var photoKey = $"{prefix}/snap-1.img";
        var receiptKey = $"{prefix}/receipts/r-1.doc";
        ownedAlone.Attach(AssetEvidence.Observed("device", photoKey, Now));
        ownedAlone.Attach(AssetEvidence.OwnershipSupported("receipt", receiptKey, Now));

        var shared = Asset.Register(TenantId.Default, subject.Id, "Couch", AssetCategory.Furniture, Now);
        var sharedPhotoKey = $"{prefix}/snap-2.img";
        shared.Attach(AssetEvidence.Observed("device", sharedPhotoKey, Now));
        shared.AddCoOwner(partner.Id, Now);

        var partnersAsset = Asset.Register(TenantId.Default, partner.Id, "Bicycle", AssetCategory.Other, Now);
        partnersAsset.AddCoOwner(subject.Id, Now);

        await using (var seed = NewContext())
        {
            seed.Customers.Add(customer);
            seed.Owners.AddRange(subject, partner);
            seed.Assets.AddRange(ownedAlone, shared, partnersAsset);
            seed.AssetPassports.AddRange(
                AssetPassport.Mint(ownedAlone, $"AP-{Guid.NewGuid():N}"[..14], Now),
                AssetPassport.Mint(shared, $"AP-{Guid.NewGuid():N}"[..14], Now));
            seed.KadariConsents.Add(KadariConsent.Grant(customer.Id, KadariConsentScope.AssetImageProcessing, Now));
            await seed.SaveChangesAsync();
        }

        foreach (var key in new[] { photoKey, receiptKey, sharedPhotoKey })
        {
            await _blobs.UploadAsync(key, Stream.Null, "image/jpeg");
        }

        return new Household(customer, partner.Id, ownedAlone, shared, partnersAsset, photoKey, receiptKey, sharedPhotoKey);
    }

    [Fact]
    public async Task Erase_removes_their_own_assets_hands_shared_ones_to_the_household_and_forgets_them()
    {
        var h = await SeedHouseholdAsync();

        EraseCustomerResponse result;
        await using (var db = NewContext())
        {
            result = await BuildSender(db).Send(new EraseCustomerCommand(h.Customer.Id.Value));
        }

        Assert.Equal((1, 1, 2), (result.AssetsErased, result.AssetsHandedOver, result.FilesDeleted));

        await using var read = NewContext();

        // Owned alone: gone from reads, files deleted and forgotten, passport no longer vouches for it.
        Assert.False(await read.Assets.AnyAsync(a => a.Id == h.OwnedAlone.Id));
        var erasedAsset = await read.Assets.IgnoreQueryFilters().Include(a => a.Evidence)
            .SingleAsync(a => a.Id == h.OwnedAlone.Id);
        Assert.All(erasedAsset.Evidence, e => Assert.Null(e.Reference));
        Assert.DoesNotContain(h.PhotoKey, _blobs.Keys);
        Assert.DoesNotContain(h.ReceiptKey, _blobs.Keys);
        var revoked = await read.AssetPassports.IgnoreQueryFilters().SingleAsync(p => p.AssetId == h.OwnedAlone.Id);
        Assert.Equal(AssetPassportStatus.Revoked, revoked.Status);

        // Shared: now the partner's, with its evidence and passport intact.
        var shared = await read.Assets.Include(a => a.CoOwners).SingleAsync(a => a.Id == h.Shared.Id);
        Assert.Equal(h.Partner, shared.OwnerId);
        Assert.Empty(shared.CoOwners);
        Assert.Contains(h.SharedPhotoKey, _blobs.Keys);
        var sharedPassport = await read.AssetPassports.SingleAsync(p => p.AssetId == h.Shared.Id);
        Assert.Equal((h.Partner, AssetPassportStatus.Active), (sharedPassport.OwnerId, sharedPassport.Status));

        // Co-owned only: they leave the partner's household.
        var partners = await read.Assets.Include(a => a.CoOwners).SingleAsync(a => a.Id == h.PartnersAsset.Id);
        Assert.Empty(partners.CoOwners);

        // Nothing left identifies them: name gone, consent withdrawn.
        var subject = await read.Owners.SingleAsync(s => s.CustomerId == h.Customer.Id);
        Assert.Equal("[erased]", subject.DisplayName);
        var consent = await read.KadariConsents.SingleAsync(c => c.CustomerId == h.Customer.Id);
        Assert.Equal(KadariConsentStatus.Withdrawn, consent.Status);
    }

    [Fact]
    public async Task Export_includes_their_assets_evidence_passports_and_consents()
    {
        var h = await SeedHouseholdAsync();

        await using var db = NewContext();
        var export = await BuildSender(db).Send(new ExportCustomerDataQuery(h.Customer.Id.Value));

        var kadari = export.Ledger;
        Assert.Equal(
            [("Bicycle", false), ("Couch", true), ("MacBook Air", true)],
            kadari.Assets.Select(a => (a.DisplayName, a.PrimaryOwner)).OrderBy(a => a.DisplayName));
        Assert.Equal(2, kadari.Assets.Single(a => a.DisplayName == "MacBook Air").Evidence.Count(e => e.HasFile));
        Assert.Equal(2, kadari.Passports.Count);
        Assert.Equal(nameof(KadariConsentScope.AssetImageProcessing), Assert.Single(kadari.Consents).Scope);
    }
}
