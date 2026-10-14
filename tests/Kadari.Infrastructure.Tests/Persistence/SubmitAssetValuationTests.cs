using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.SubmitValuation;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the professional-valuation path against real Postgres (D10): a valuer's independent valuation lifts
/// an already-ownership-supported asset to ValueSupported, records the independent value without lifting an
/// asset that lacks ownership evidence, and 404s a missing asset.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SubmitAssetValuationTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AsStaff();   // a Verifier: holds AssetValuate and bypasses ownership
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<Guid> SeedAssetAsync(Action<Asset> attachEvidence)
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Diamond ring", AssetCategory.Jewellery, Now);
        asset.Attach(AssetEvidence.Observed("device", "blob/ring.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.6, Now));
        attachEvidence(asset);

        await using (var db = NewContext())
        {
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        return asset.Id.Value;
    }

    [Fact]
    public async Task A_valuation_lifts_an_ownership_supported_asset_to_value_supported()
    {
        var assetId = await SeedAssetAsync(a =>
            a.Attach(AssetEvidence.OwnershipSupported("receipt", "blob/receipt.pdf", Now)));

        SubmitAssetValuationResponse response;
        await using (var db = NewContext())
        {
            response = await BuildSender(db).Send(new SubmitAssetValuationCommand(
                assetId, 80_000_00, "ZAR", "Acme Valuers", "cert/AV-2026-001"));
        }

        Assert.Equal(nameof(AssetAssuranceLevel.ValueSupported), response.Assurance);

        await using var verify = NewContext();
        var asset = await verify.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == new AssetId(assetId));
        var value = asset.Evidence.Single(e => e.Kind == AssetEvidenceKind.Value);
        Assert.Equal(EvidenceStrength.Independent, value.Strength);
        Assert.Equal("Acme Valuers", value.Source);
    }

    [Fact]
    public async Task A_valuation_without_ownership_records_value_but_stays_identified()
    {
        var assetId = await SeedAssetAsync(_ => { });   // image + identification only

        SubmitAssetValuationResponse response;
        await using (var db = NewContext())
        {
            response = await BuildSender(db).Send(new SubmitAssetValuationCommand(
                assetId, 80_000_00, "ZAR", "Acme Valuers", "cert/AV-1"));
        }

        Assert.Equal(nameof(AssetAssuranceLevel.Identified), response.Assurance);

        await using var verify = NewContext();
        var asset = await verify.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == new AssetId(assetId));
        Assert.Contains(asset.Evidence, e => e.Kind == AssetEvidenceKind.Value && e.Strength == EvidenceStrength.Independent);
    }

    [Fact]
    public async Task Valuing_a_missing_asset_is_not_found()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => BuildSender(context).Send(
            new SubmitAssetValuationCommand(Guid.CreateVersion7(), 1_000_00, "ZAR", "Acme Valuers", "cert/AV-x")));
    }
}
