using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Application.Features.GetConsents;
using Kadari.Application.Features.GrantConsent;
using Kadari.Application.Features.WithdrawConsent;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.ValueObjects;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the Kadari consent gate against real Postgres (spec §6, D8): a cross-border enricher is used
/// only once the customer has granted the distinct cross-border-AI consent; without it the still stays
/// on-shore and capture falls back to the on-device detections. Capture itself is refused until the customer
/// grants image processing, and stops again once they withdraw it (AS-02). Also covers the grant upsert.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class KadariConsentTests(PostgresFixture fixture) : InfraTest(fixture)
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
        services.AddSingleton<IBlobStorage>(new QuietBlobStorage());
        services.AddSingleton<IVisionEnricher>(new CrossBorderEnricher());
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task<Customer> SeedCustomerAsync(string mobile, string email, bool consentingToCapture = true)
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var customer = Customer.Register("Jane", "Doe", mobile, "ZA", email: email);
        await using (var seed = NewContext())
        {
            if (consentingToCapture)
            {
                seed.AddCustomerConsentingToCapture(customer);
            }
            else
            {
                seed.Customers.Add(customer);
            }
            await seed.SaveChangesAsync();
        }

        return customer;
    }

    private async Task<AnalyzeSnapResponse> CaptureAsync(Customer customer)
    {
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, [0xFF, 0xD8, 0xFF, 0xE0]);   // a JPEG header (GL-18 sniffs it)
        var image = new SnapImage(tempPath, "image/jpeg", 4);
        try
        {
            await using var db = NewContext();
            return await BuildSender(db).Send(new AnalyzeSnapCommand(
                customer.Id.Value, image, [new DetectedObject("tv", 0.94, 0, 0, 1, 1)], "Lounge"));
        }
        finally
        {
            await image.DisposeAsync();
        }
    }

    [Fact]
    public async Task Without_image_processing_consent_capture_is_refused()
    {
        var customer = await SeedCustomerAsync(
            "+27820000054", "jane.noimage@example.com", consentingToCapture: false);

        var refused = await Assert.ThrowsAsync<ConsentRequiredException>(() => CaptureAsync(customer));

        Assert.Equal(nameof(KadariConsentScope.AssetImageProcessing), refused.Scope);
    }

    [Fact]
    public async Task Withdrawing_image_processing_consent_stops_capture()
    {
        var customer = await SeedCustomerAsync("+27820000055", "jane.withdraw@example.com");
        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new WithdrawKadariConsentCommand(
                customer.Id.Value, KadariConsentScope.AssetImageProcessing));
        }

        await Assert.ThrowsAsync<ConsentRequiredException>(() => CaptureAsync(customer));

        await using var read = NewContext();
        var consents = await BuildSender(read).Send(new GetKadariConsentsQuery(customer.Id.Value));
        Assert.False(consents.Single(c => c.Scope == nameof(KadariConsentScope.AssetImageProcessing)).Granted);
    }

    [Fact]
    public async Task Without_cross_border_consent_the_still_is_not_sent_to_the_off_shore_model()
    {
        var customer = await SeedCustomerAsync("+27820000051", "jane.noconsent@example.com");

        var response = await CaptureAsync(customer);

        Assert.False(response.Enriched);   // fell back to on-device detections

        await using var verify = NewContext();
        var assetId = response.AssetIds.Single();
        var asset = await verify.Assets.SingleAsync(a => a.Id == new AssetId(assetId));
        Assert.Equal("tv", asset.DisplayName);   // the raw detection label, not the model's identification
    }

    [Fact]
    public async Task With_cross_border_consent_the_model_enriches_the_capture()
    {
        var customer = await SeedCustomerAsync("+27820000052", "jane.consent@example.com");

        await using (var db = NewContext())
        {
            await BuildSender(db).Send(new GrantKadariConsentCommand(
                customer.Id.Value, KadariConsentScope.CrossBorderAiEnrichment));
        }

        var response = await CaptureAsync(customer);

        Assert.True(response.Enriched);

        await using var verify = NewContext();
        var asset = await verify.Assets.SingleAsync(a => a.Id == new AssetId(response.AssetIds.Single()));
        Assert.Equal("Samsung 65\" TV", asset.DisplayName);   // the model's identification
    }

    [Fact]
    public async Task Granting_the_same_scope_twice_upserts_one_row()
    {
        var customer = await SeedCustomerAsync("+27820000053", "jane.upsert@example.com");

        GrantKadariConsentResponse first;
        GrantKadariConsentResponse second;
        await using (var db = NewContext())
        {
            first = await BuildSender(db).Send(new GrantKadariConsentCommand(
                customer.Id.Value, KadariConsentScope.AssetImageProcessing));
        }
        await using (var db = NewContext())
        {
            second = await BuildSender(db).Send(new GrantKadariConsentCommand(
                customer.Id.Value, KadariConsentScope.AssetImageProcessing));
        }

        Assert.Equal(first.ConsentId, second.ConsentId);

        await using var verify = NewContext();
        Assert.Equal(1, await verify.KadariConsents.CountAsync(c => c.CustomerId == customer.Id));
    }
}

file sealed class QuietBlobStorage : IBlobStorage
{
    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

// A stand-in for Gemini: cross-border, and it would identify the item if allowed to run.
file sealed class CrossBorderEnricher : IVisionEnricher
{
    public bool IsCrossBorder => true;

    public Task<VisionEnrichmentResult> EnrichAsync(VisionEnrichmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new VisionEnrichmentResult(true, new List<IdentifiedItem>
        {
            new("tv", 0.94, 0, 0, 1, 1, "Samsung 65\" TV", AssetCategory.Electronics, "good", null, Money.Zar(2_749_900), 0),
        }));
}
