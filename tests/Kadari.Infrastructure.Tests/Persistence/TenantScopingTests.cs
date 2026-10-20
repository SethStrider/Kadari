using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.AssignTenant;
using Kadari.Application.Features.CreateInsurerTenant;
using Kadari.Application.Features.GetLedger;
using Kadari.Application.Features.SeedDefaultTenant;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves Kadari tenancy against real Postgres: the default-tenant seed is idempotent, and a ledger read
/// narrows to the caller's tenant so a foreign-tenant asset owned by the same subject is filtered out.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantScopingTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AddSingleton<IBlobStorage>(new SilentBlobStorage());
        services.AddSingleton<IVisionEnricher>(new OneItemEnricher());
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Seeding_the_default_tenant_is_idempotent()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        SeedDefaultTenantResponse first;
        SeedDefaultTenantResponse second;
        await using (var db = NewContext())
        {
            first = await BuildSender(db).Send(new SeedDefaultTenantCommand());
        }
        await using (var db = NewContext())
        {
            second = await BuildSender(db).Send(new SeedDefaultTenantCommand());
        }

        Assert.False(second.Created);   // the second boot found it already there
        Assert.True(first.Created || !second.Created);

        await using (var verify = NewContext())
        {
            Assert.Equal(1, await verify.Tenants.CountAsync(t => t.Id == TenantId.Default));
        }
    }

    [Fact]
    public async Task A_ledger_read_excludes_a_foreign_tenants_asset()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Jane", "Doe", "+27820000041", "ZA", email: "jane.tenant@example.com");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        // Capture one asset in the platform (Default) tenant; this also creates the backing subject.
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Lounge"));
        }
        finally
        {
            await image.DisposeAsync();
        }

        // Add a second asset for the same owner subject, but stamped to a different (insurer) tenant.
        await using (var db = NewContext())
        {
            var ownerId = await db.Owners.Where(s => s.CustomerId == customer.Id).Select(s => s.Id).SingleAsync();
            db.Assets.Add(Asset.Register(TenantId.New(), ownerId, "Foreign tenant TV", AssetCategory.Electronics, DateTimeOffset.UnixEpoch));
            await db.SaveChangesAsync();
        }

        LedgerDto ledger;
        await using (var db = NewContext())
        {
            ledger = await BuildSender(db).Send(new GetLedgerQuery(customer.Id.Value));
        }

        // The customer is on the platform tenant, so only the Default-tenant asset is visible.
        Assert.Equal(1, ledger.AssetCount);
    }

    [Fact]
    public async Task An_insured_customer_still_sees_their_own_ledger()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Sipho", "Dube", "+27820000042", "ZA");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Lounge"));
        }
        finally
        {
            await image.DisposeAsync();
        }

        // Moving the customer onto an insurer's book re-stamps their assets to the insurer's tenant.
        await using (var db = NewContext())
        {
            var sender = BuildSender(db);
            var insurer = await sender.Send(new CreateInsurerTenantCommand("Insurer for scoping test", Branding: null));
            await sender.Send(new AssignCustomerToTenantCommand(customer.Id.Value, insurer.TenantId));
        }

        LedgerDto ledger;
        await using (var db = NewContext())
        {
            ledger = await BuildSender(db).Send(new GetLedgerQuery(customer.Id.Value));
        }

        Assert.Equal(1, ledger.AssetCount);
    }
}

file sealed class SilentBlobStorage : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class OneItemEnricher : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.94, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, null, 0),
        }));
}
