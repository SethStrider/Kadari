using Microsoft.EntityFrameworkCore;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the Kadari aggregates round-trip against real Postgres: an Asset with its child evidence
/// set (owned Money on value evidence, shadow FK), the derived assurance recomputing after reload, and
/// the Tenant partition.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class KadariPersistenceTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Asset_with_evidence_round_trips_and_recomputes_assurance()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var tenant = TenantId.New();
        var owner = OwnerId.New();
        var asset = Asset.Register(tenant, owner, "Samsung 65\" TV", AssetCategory.Electronics, Now, roomLabel: "Living room");
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.97, Now));
        asset.Attach(AssetEvidence.OwnershipSupported("receipt", "blob/receipt.pdf", Now));
        asset.Attach(AssetEvidence.ValueSupported("valuer", Money.Zar(2_749_900), "blob/valuation.pdf", Now));

        await using (var db = NewContext())
        {
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var reloaded = await db.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == asset.Id);

            Assert.Equal(tenant, reloaded.TenantId);
            Assert.Equal(owner, reloaded.OwnerId);
            Assert.Equal("Living room", reloaded.RoomLabel);
            Assert.Equal(4, reloaded.Evidence.Count);

            var value = reloaded.Evidence.Single(e => e.Kind == AssetEvidenceKind.Value);
            Assert.Equal(EvidenceStrength.Independent, value.Strength);
            Assert.Equal(Money.Zar(2_749_900), value.Amount);

            var identification = reloaded.Evidence.Single(e => e.Kind == AssetEvidenceKind.Identification);
            Assert.Equal(0.97, identification.Confidence);

            // Assurance is derived, not stored — it recomputes from the reloaded evidence set.
            Assert.Equal(AssetAssuranceLevel.ValueSupported, reloaded.Assurance);
        }
    }

    [Fact]
    public async Task Value_amount_is_null_for_non_value_evidence()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "MacBook Air", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));

        await using (var db = NewContext())
        {
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var reloaded = await db.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == asset.Id);
            Assert.Null(reloaded.Evidence.Single().Amount);
            Assert.Equal(AssetAssuranceLevel.Observed, reloaded.Assurance);
        }
    }

    [Fact]
    public async Task AssetPassport_round_trips_with_its_snapshot_and_owned_value()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Samsung 65\" TV", AssetCategory.Electronics, Now);
        asset.Attach(AssetEvidence.Observed("device", "blob/frame.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.97, Now));
        asset.Attach(AssetEvidence.ValueEstimated("gemini", Money.Zar(1_500_00), Now));
        var passport = AssetPassport.Mint(asset, "AP-ROUNDTRIP", Now);

        await using (var db = NewContext())
        {
            db.Assets.Add(asset);
            db.AssetPassports.Add(passport);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var reloaded = await db.AssetPassports.SingleAsync(p => p.Id == passport.Id);

            Assert.Equal(asset.Id, reloaded.AssetId);
            Assert.Equal(asset.OwnerId, reloaded.OwnerId);
            Assert.Equal(TenantId.Default, reloaded.TenantId);
            Assert.Equal("AP-ROUNDTRIP", reloaded.VerificationId);
            Assert.Equal("Samsung 65\" TV", reloaded.DisplayName);
            Assert.Equal(AssetAssuranceLevel.Identified, reloaded.Assurance);
            Assert.Equal(Money.Zar(1_500_00), reloaded.ReplacementValue);
            Assert.Equal(3, reloaded.EvidenceCount);
            Assert.Equal(AssetPassportStatus.Active, reloaded.Status);
        }
    }

    [Fact]
    public async Task Tenant_round_trips()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var insurer = Tenant.CreateInsurer("Acme Insurance");
        insurer.Suspend();

        await using (var db = NewContext())
        {
            db.Tenants.Add(insurer);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var reloaded = await db.Tenants.SingleAsync(t => t.Id == insurer.Id);
            Assert.Equal("Acme Insurance", reloaded.Name);
            Assert.Equal(TenantKind.Insurer, reloaded.Kind);
            Assert.Equal(TenantStatus.Suspended, reloaded.Status);
        }
    }
}
