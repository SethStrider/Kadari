using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the capture command against real Postgres: a snap stores its evidence image, enriches the
/// detections, and registers each identified item as an Asset (with image + identification + value
/// evidence) owned by the customer's backing subject.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AnalyzeSnapTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(
        KadariDbContext context, IBlobStorage blobs, IVisionEnricher enricher, bool realUnitOfWork = false)
    {
        var services = new ServiceCollection();
        if (realUnitOfWork)
        {
            services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(context));
        }

        services.AddApplication();
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton(blobs);
        services.AddSingleton(enricher);
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Analyze_stores_the_image_and_registers_identified_assets()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Jane", "Doe", "+27820000001", "ZA", email: "jane.snap@example.com");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        var items = new List<IdentifiedItem>
        {
            new("tv", 0.94, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, Money.Zar(2_749_900), 0),
        };
        var blobs = new RecordingBlobStorage();
        var enricher = new CannedVisionEnricher(new VisionEnrichmentResult(true, items));

        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);

        AnalyzeSnapResponse response;
        await using (var db = NewContext())
        {
            var sender = BuildSender(db, blobs, enricher);
            response = await sender.Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Living room"));
        }
        await image.DisposeAsync();

        Assert.True(response.Enriched);
        var assetId = Assert.Single(response.AssetIds);
        Assert.Single(blobs.Stored);

        await using (var verify = NewContext())
        {
            var asset = await verify.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == new AssetId(assetId));

            Assert.Equal("Samsung 65\" TV", asset.DisplayName);
            Assert.Equal("Living room", asset.RoomLabel);
            Assert.Equal(AssetAssuranceLevel.Identified, asset.Assurance);
            Assert.Contains(asset.Evidence, e => e.Kind == AssetEvidenceKind.Image);
            Assert.Contains(asset.Evidence, e => e.Kind == AssetEvidenceKind.Value);

            var subject = await verify.Owners.SingleAsync(s => s.CustomerId == customer.Id);
            Assert.Equal(subject.Id, asset.OwnerId);
        }
    }

    /// <summary>AS-16: a snap that finds nothing stores no photo, so no home image is left that no asset refers to.</summary>
    [Fact]
    public async Task A_snap_that_finds_nothing_stores_no_photo()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Empty", "Room", $"+2782{Random.Shared.Next(1000000, 9999999)}", "ZA");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        var blobs = new RecordingBlobStorage();
        var enricher = new CannedVisionEnricher(new VisionEnrichmentResult(true, []));
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);
        await using var image = new SnapImage(tempPath, "image/jpeg", 4);

        AnalyzeSnapResponse response;
        await using (var db = NewContext())
        {
            response = await BuildSender(db, blobs, enricher).Send(
                new AnalyzeSnapCommand(customer.Id.Value, image, [], "Empty room"));
        }

        Assert.Empty(response.AssetIds);
        Assert.Empty(blobs.Stored);
    }

    /// <summary>
    /// GL-35: a transient database failure makes the strategy re-run the whole capture. The still must be stored
    /// once and the (paid) vision model called once; the assets are still written exactly once.
    /// </summary>
    [Fact]
    public async Task A_retried_capture_stores_the_still_and_calls_the_model_once()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Retry", "Capture", $"+2782{Random.Shared.Next(1000000, 9999999)}", "ZA");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        var items = new List<IdentifiedItem>
        {
            new("tv", 0.94, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, Money.Zar(2_749_900), 0),
        };
        var blobs = new RecordingBlobStorage();
        var enricher = new CountingVisionEnricher(new VisionEnrichmentResult(true, items));

        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);
        var image = new SnapImage(tempPath, "image/jpeg", 4);

        var dropFirstSave = new DropFirstSave();
        AnalyzeSnapResponse response;
        await using (var db = RetryingContext(dropFirstSave))
        {
            var sender = BuildSender(db, blobs, enricher, realUnitOfWork: true);
            response = await sender.Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Lounge"));
        }
        await image.DisposeAsync();

        Assert.Equal(2, dropFirstSave.Saves);        // the asset save failed once, so the transaction was retried
        Assert.Equal(1, enricher.Calls);             // …but the model was billed once
        Assert.Single(blobs.Stored);                 // …and the still stored once, under the key returned
        await using var verify = NewContext();
        var owner = await verify.Owners.SingleAsync(x => x.CustomerId == customer.Id);
        var assets = await verify.Assets.Where(a => a.OwnerId == owner.Id).Select(a => a.Id).ToListAsync();
        Assert.Equal([new AssetId(response.AssetIds.Single())], assets);   // written once, and it's the one returned
    }

    private KadariDbContext RetryingContext(IInterceptor dropFirstSave) =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.ExecutionStrategy(d => new RetryOnTimeout(d)))
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System), dropFirstSave)
            .Options);

    [Fact]
    public async Task Analyze_for_a_mobile_first_customer_labels_the_subject_by_mobile()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        // A customer auto-registered from OTP has no name yet (mobile-first). Capture must still work —
        // the backing subject falls back to the mobile as its display name until the profile is completed.
        var customer = Customer.ProvisionForMobile("+27820000009", "ZA");
        await using (var db = NewContext())
        {
            db.AddCustomerConsentingToCapture(customer);
            await db.SaveChangesAsync();
        }

        var items = new List<IdentifiedItem>
        {
            new("tv", 0.9, 0, 0, 1, 1, "Samsung TV", AssetCategory.Electronics, null, null, null, 0),
        };
        var blobs = new RecordingBlobStorage();
        var enricher = new CannedVisionEnricher(new VisionEnrichmentResult(true, items));

        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);

        AnalyzeSnapResponse response;
        await using (var db = NewContext())
        {
            var sender = BuildSender(db, blobs, enricher);
            response = await sender.Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.9, 0, 0, 1, 1)], "Lounge"));
        }
        await image.DisposeAsync();

        Assert.Single(response.AssetIds);

        await using (var verify = NewContext())
        {
            var subject = await verify.Owners.SingleAsync(s => s.CustomerId == customer.Id);
            Assert.Equal("+27820000009", subject.DisplayName);
        }
    }
}

file sealed class RecordingBlobStorage : IBlobStorage
{
    public Dictionary<string, byte[]> Stored { get; } = [];

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        Stored[key] = ms.ToArray();
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(Stored[key]));

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Stored.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Stored.Remove(key);
        return Task.CompletedTask;
    }
}

file sealed class CannedVisionEnricher(VisionEnrichmentResult result) : IVisionEnricher
{
    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(result);
}

file sealed class CountingVisionEnricher(VisionEnrichmentResult result) : IVisionEnricher
{
    public int Calls;

    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(result);
    }
}

/// <summary>Retries on the simulated transient failure, as Npgsql's strategy does on a dropped connection.</summary>
file sealed class RetryOnTimeout(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10))
{
    protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
}

/// <summary>
/// The connection drops during the first save that writes assets — the one that comes after the still is stored and
/// the model has answered, so the retry would redo that external work.
/// </summary>
file sealed class DropFirstSave : SaveChangesInterceptor
{
    public int Saves;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var writesAssets = eventData.Context!.ChangeTracker.Entries<Asset>().Any(e => e.State == EntityState.Added);
        return writesAssets && ++Saves == 1
            ? throw new TimeoutException("simulated transient failure during SaveChanges")
            : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
