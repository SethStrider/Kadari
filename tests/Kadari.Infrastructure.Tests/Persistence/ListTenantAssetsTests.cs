using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Common.Pagination;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.ListTenantAssets;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the insurer cohort asset list against real Postgres: it pages a tenant's own assets with their
/// derived headline (assurance, best value), excludes other tenants, and 404s an unknown tenant.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ListTenantAssetsTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private static Asset ValuedAsset(TenantId tenant, OwnerId owner, string name, AssetCategory category, Money value)
    {
        var asset = Asset.Register(tenant, owner, name, category, Now);
        asset.Attach(AssetEvidence.Observed("device", $"blob/{name}.jpg", Now));
        asset.Attach(AssetEvidence.Identified("gemini", 0.9, Now));
        asset.Attach(AssetEvidence.ValueEstimated("ai-baseline", value, Now));
        return asset;
    }

    private async Task<Tenant> SeedCohortAsync()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var insurer = Tenant.CreateInsurer("Acme Insurance");
        var alice = OwnerId.New();
        var bob = OwnerId.New();

        await using (var db = NewContext())
        {
            db.Tenants.Add(insurer);
            db.Assets.Add(ValuedAsset(insurer.Id, alice, "Apple TV", AssetCategory.Electronics, Money.Zar(2_000_00)));
            db.Assets.Add(ValuedAsset(insurer.Id, alice, "Bosch Fridge", AssetCategory.Furniture, Money.Zar(15_000_00)));
            db.Assets.Add(ValuedAsset(insurer.Id, bob, "Canon Camera", AssetCategory.Electronics, Money.Zar(30_000_00)));
            // Another tenant's asset must not appear.
            db.Assets.Add(ValuedAsset(TenantId.Default, OwnerId.New(), "Zebra Rug", AssetCategory.Other, Money.Zar(9_999_00)));
            await db.SaveChangesAsync();
        }

        return insurer;
    }

    [Fact]
    public async Task Lists_the_tenants_own_assets_with_their_derived_headline()
    {
        var insurer = await SeedCohortAsync();

        PagedResult<TenantAssetRowDto> result;
        await using (var db = NewContext())
        {
            result = await BuildSender(db).Send(new ListTenantAssetsQuery { TenantId = insurer.Id.Value, PageSize = 50 });
        }

        Assert.Equal(3, result.TotalCount);              // only the insurer's three, not the Default-tenant rug
        Assert.Equal(3, result.Items.Count);
        Assert.DoesNotContain(result.Items, r => r.DisplayName == "Zebra Rug");

        var camera = result.Items.Single(r => r.DisplayName == "Canon Camera");
        Assert.Equal("Identified", camera.Assurance);    // image + identification (value is self-asserted)
        Assert.Equal(30_000_00, camera.BestValueMinor);
    }

    [Fact]
    public async Task Pages_the_cohort()
    {
        var insurer = await SeedCohortAsync();

        PagedResult<TenantAssetRowDto> page1;
        PagedResult<TenantAssetRowDto> page2;
        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            // Default sort is by display name ascending, so page order is deterministic.
            page1 = await sender.Send(new ListTenantAssetsQuery { TenantId = insurer.Id.Value, Page = 1, PageSize = 2 });
            page2 = await sender.Send(new ListTenantAssetsQuery { TenantId = insurer.Id.Value, Page = 2, PageSize = 2 });
        }

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal("Apple TV", page1.Items[0].DisplayName);
        Assert.True(page1.HasNext);

        Assert.Single(page2.Items);
        Assert.Equal("Canon Camera", page2.Items[0].DisplayName);
        Assert.False(page2.HasNext);
    }

    [Fact]
    public async Task An_unknown_tenant_is_not_found()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context).Send(new ListTenantAssetsQuery { TenantId = Guid.CreateVersion7() }));
    }
}
