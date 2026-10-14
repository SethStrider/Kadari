using System.Diagnostics.Metrics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the daily vision quota (AS-09) against real Postgres: once a customer has used today's metered calls,
/// further captures still succeed but fall back to the on-device detections, and nothing more is billed.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class VisionQuotaTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private readonly MeteredEnricher _enricher = new();

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private ISender BuildSender(KadariDbContext context)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AsStaff();
        services.Configure<VisionQuotaOptions>(o => o.DailyCallsPerCustomer = 2);
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IBlobStorage>(new NullBlobStorage());
        services.AddSingleton<IVisionEnricher>(_enricher);
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<AnalyzeSnapResponse> CaptureAsync(Customer customer)
    {
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);
        await using var image = new SnapImage(tempPath, "image/jpeg", 4);
        await using var db = NewContext();
        return await BuildSender(db).Send(new AnalyzeSnapCommand(
            customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Lounge"));
    }

    [Fact]
    public async Task Past_the_daily_cap_capture_still_works_but_the_model_is_not_called()
    {
        await using (var migrate = NewContext())
        {
            await migrate.Database.MigrateAsync();
        }

        var customer = Customer.Register("Jane", "Doe", $"+2782{Random.Shared.Next(1000000, 9999999)}", "ZA");
        await using (var seed = NewContext())
        {
            seed.AddCustomerConsentingToCapture(customer);
            await seed.SaveChangesAsync();
        }

        var outcomes = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Kadari.Capture")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "enrichment")
                {
                    lock (outcomes) outcomes.Add((string)tag.Value!);
                }
            }
        });
        listener.Start();

        var results = new List<bool>();
        for (var i = 0; i < 3; i++)
        {
            results.Add((await CaptureAsync(customer)).Enriched);
        }

        Assert.Equal([true, true, false], results);
        Assert.Equal(2, _enricher.Calls);
        Assert.Contains("quota_reached", outcomes);   // AS-27: the fallback is visible as a metric
        await using var read = NewContext();
        Assert.Equal(2, (await read.VisionUsages.SingleAsync(u => u.CustomerId == customer.Id)).Calls);
    }
}

internal sealed class MeteredEnricher : IVisionEnricher
{
    public int Calls;

    public bool IsMetered => true;

    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.94, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, Money.Zar(2_749_900), 0),
        }));
    }
}

file sealed class NullBlobStorage : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
