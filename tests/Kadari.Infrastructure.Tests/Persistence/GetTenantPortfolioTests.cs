using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.GetTenantPortfolio;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the insurer tenant-portfolio read against real Postgres: it aggregates a tenant's whole cohort
/// across customers (and only that tenant's), an unknown tenant is a 404, and a real but empty tenant is a
/// zeroed portfolio rather than an error.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class GetTenantPortfolioTests(PostgresFixture fixture) : InfraTest(fixture)
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

    [Fact]
    public async Task Portfolio_aggregates_only_the_tenants_own_cohort()
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
            db.Assets.Add(ValuedAsset(insurer.Id, alice, "TV", AssetCategory.Electronics, Money.Zar(2_749_900)));
            db.Assets.Add(ValuedAsset(insurer.Id, bob, "Ring", AssetCategory.Jewellery, Money.Zar(8_000_000)));
            // A different tenant's asset must NOT leak into this portfolio.
            db.Assets.Add(ValuedAsset(TenantId.Default, OwnerId.New(), "Laptop", AssetCategory.Electronics, Money.Zar(1_000_000)));
            await db.SaveChangesAsync();
        }

        TenantPortfolioDto portfolio;
        await using (var db = NewContext())
        {
            portfolio = await BuildSender(db).Send(new GetTenantPortfolioQuery(insurer.Id.Value));
        }

        Assert.Equal(2, portfolio.AssetCount);                                  // only the insurer's two
        Assert.Equal(2, portfolio.PolicyholderCount);                          // alice + bob
        Assert.Equal(2_749_900 + 8_000_000, portfolio.RecordedReplacementExposureMinor);
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
            BuildSender(context).Send(new GetTenantPortfolioQuery(Guid.CreateVersion7())));
    }

    [Fact]
    public async Task A_real_tenant_with_no_assets_is_a_zeroed_portfolio()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var insurer = Tenant.CreateInsurer("Empty Mutual");
        await using (var db = NewContext())
        {
            db.Tenants.Add(insurer);
            await db.SaveChangesAsync();
        }

        await using var context = NewContext();
        var portfolio = await BuildSender(context).Send(new GetTenantPortfolioQuery(insurer.Id.Value));

        Assert.Equal(0, portfolio.AssetCount);
        Assert.Equal(0, portfolio.RecordedReplacementExposureMinor);
    }
}
