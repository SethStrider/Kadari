using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.AssignTenant;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the tenancy-assignment loop against real Postgres: assigning a customer to an insurer tenant
/// re-stamps their existing assets and makes future captures inherit the tenant, so the whole cohort lands in
/// the insurer's partition. Also covers the membership upsert and the 404s.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AssignCustomerToTenantTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AddSingleton<IBlobStorage>(new SilentBlob());
        services.AddSingleton<IVisionEnricher>(new OneItem());
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
        await using (var seed = NewContext())
        {
            seed.AddCustomerConsentingToCapture(customer);
            await seed.SaveChangesAsync();
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
            var response = await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.9, 0, 0, 1, 1)], "Lounge"));
            return response.AssetIds.Single();
        }
        finally
        {
            await image.DisposeAsync();
        }
    }

    [Fact]
    public async Task Assigning_restamps_existing_assets_and_future_captures_inherit_the_tenant()
    {
        var customer = await SeedCustomerAsync("+27820000081", "jane.assign@example.com");
        var firstAsset = await CaptureAsync(customer);   // captured on the default tenant

        var insurer = Tenant.CreateInsurer("Acme Insurance");
        await using (var db = NewContext())
        {
            db.Tenants.Add(insurer);
            await db.SaveChangesAsync();
        }

        AssignCustomerToTenantResponse assignment;
        await using (var db = NewContext())
        {
            assignment = await BuildSender(db).Send(new AssignCustomerToTenantCommand(customer.Id.Value, insurer.Id.Value));
        }

        Assert.Equal(1, assignment.AssetsReassigned);

        // The existing asset moved onto the insurer tenant.
        await using (var verify = NewContext())
        {
            var asset = await verify.Assets.SingleAsync(a => a.Id == new AssetId(firstAsset));
            Assert.Equal(insurer.Id, asset.TenantId);
            Assert.Equal(1, await verify.TenantMemberships.CountAsync(m => m.CustomerId == customer.Id));
        }

        // A new capture now inherits the insurer tenant via the membership.
        var secondAsset = await CaptureAsync(customer);
        await using (var verify = NewContext())
        {
            var asset = await verify.Assets.SingleAsync(a => a.Id == new AssetId(secondAsset));
            Assert.Equal(insurer.Id, asset.TenantId);

            // The whole cohort now sits in the insurer partition, none on the default.
            Assert.Equal(2, await verify.Assets.CountAsync(a => a.TenantId == insurer.Id));
            Assert.Equal(0, await verify.Assets.CountAsync(a => a.TenantId == TenantId.Default));
        }
    }

    [Fact]
    public async Task Assigning_twice_upserts_one_membership()
    {
        var customer = await SeedCustomerAsync("+27820000082", "jane.twice@example.com");
        var insurer = Tenant.CreateInsurer("Acme Insurance");
        await using (var db = NewContext())
        {
            db.Tenants.Add(insurer);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new AssignCustomerToTenantCommand(customer.Id.Value, insurer.Id.Value));
        }
        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new AssignCustomerToTenantCommand(customer.Id.Value, insurer.Id.Value));
        }

        await using var verify = NewContext();
        Assert.Equal(1, await verify.TenantMemberships.CountAsync(m => m.CustomerId == customer.Id));
    }

    [Fact]
    public async Task An_unknown_tenant_is_not_found()
    {
        var customer = await SeedCustomerAsync("+27820000083", "jane.404@example.com");

        await using var context = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildSender(context).Send(new AssignCustomerToTenantCommand(customer.Id.Value, Guid.CreateVersion7())));
    }
}

file sealed class SilentBlob : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class OneItem : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.9, 0, 0, 1, 1, "Samsung TV", AssetCategory.Electronics, "good", null, Money.Zar(2_000_00), 0),
        }));
}
