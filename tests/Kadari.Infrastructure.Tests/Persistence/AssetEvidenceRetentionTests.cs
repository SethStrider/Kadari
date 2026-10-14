using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Abstractions.Storage;
using Kadari.Application.Features.PurgeEphemeralFrames;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;
using Kadari.Infrastructure.Persistence.Interceptors;

namespace Kadari.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the two-tier retention sweep against real Postgres (D8/POPIA): ephemeral scan frames past their
/// window are purged (blob deleted, reference cleared, marked purged) while canonical evidence images —
/// kept on the insurance basis — are left intact.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AssetEvidenceRetentionTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private static readonly DateTimeOffset Base = DateTimeOffset.UnixEpoch;

    private KadariDbContext NewContext() =>
        new(new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new EntityConventionsInterceptor(TimeProvider.System))
            .Options);

    private static ISender BuildSender(KadariDbContext context, IBlobStorage blobs, TimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddLogging();
        services.AsStaff();
        services.AddSingleton<IKadariDbContext>(context);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton(blobs);
        services.AddSingleton(clock);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Sweep_purges_expired_frames_and_keeps_canonical_images()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "Samsung 65\" TV", AssetCategory.Electronics, Base);
        asset.Attach(AssetEvidence.Observed("device", "blob/canonical.jpg", Base));
        asset.Attach(AssetEvidence.EphemeralFrame("device", "blob/frame.jpg", Base, retentionUntil: Base.AddDays(7)));

        await using (var db = NewContext())
        {
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        var blobs = new DeletingBlobStorage();
        var clock = new FixedClock(Base.AddDays(30));   // well past the frame's window

        PurgeEphemeralFramesResponse response;
        await using (var db = NewContext())
        {
            response = await BuildSender(db, blobs, clock).Send(new PurgeEphemeralFramesCommand());
        }

        Assert.Equal(1, response.Assets);
        Assert.Equal(1, response.Frames);
        Assert.Contains("blob/frame.jpg", blobs.Deleted);
        Assert.DoesNotContain("blob/canonical.jpg", blobs.Deleted);

        await using (var verify = NewContext())
        {
            var reloaded = await verify.Assets.Include(a => a.Evidence).SingleAsync(a => a.Id == asset.Id);

            var frame = reloaded.Evidence.Single(e => e.IsEphemeral);
            Assert.NotNull(frame.PurgedAt);
            Assert.Null(frame.Reference);

            var canonical = reloaded.Evidence.Single(e => !e.IsEphemeral);
            Assert.Equal("blob/canonical.jpg", canonical.Reference);
        }
    }

    [Fact]
    public async Task Sweep_leaves_a_frame_inside_its_retention_window()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
        }

        var asset = Asset.Register(TenantId.Default, OwnerId.New(), "MacBook", AssetCategory.Electronics, Base);
        asset.Attach(AssetEvidence.EphemeralFrame("device", "blob/fresh.jpg", Base, retentionUntil: Base.AddDays(7)));

        await using (var db = NewContext())
        {
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        var blobs = new DeletingBlobStorage();
        var clock = new FixedClock(Base.AddDays(2));   // still inside the window

        PurgeEphemeralFramesResponse response;
        await using (var db = NewContext())
        {
            response = await BuildSender(db, blobs, clock).Send(new PurgeEphemeralFramesCommand());
        }

        Assert.Equal(0, response.Frames);
        Assert.Empty(blobs.Deleted);
    }
}

file sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

file sealed class DeletingBlobStorage : IBlobStorage
{
    public List<string> Deleted { get; } = [];

    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Deleted.Add(key);
        return Task.CompletedTask;
    }
}
