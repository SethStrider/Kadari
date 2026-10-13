using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadari.Infrastructure.Storage;

/// <summary>
/// Ensures the configured blob bucket exists at start-up. Idempotent — an existing bucket is left alone (a
/// persisted MinIO volume, or one an ops team provisioned in production) — so it is safe on every boot, and
/// it only issues a create when the bucket is genuinely missing, so it never needs <c>CreateBucket</c> rights
/// where the bucket is provisioned out-of-band. A failure (storage not ready, or the create not permitted) is
/// logged, never fatal: the host must still start. Mirrors <see cref="Persistence.Seeding.CatalogueSeeder"/>.
/// </summary>
internal sealed class BlobStorageInitializer(
    IAmazonS3 client, IOptions<S3Options> options, ILogger<BlobStorageInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucket = options.Value.BucketName;
        try
        {
            if (await AmazonS3Util.DoesS3BucketExistV2Async(client, bucket))
            {
                logger.LogInformation("Blob bucket {Bucket} already present.", bucket);
                return;
            }

            await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, cancellationToken);
            logger.LogInformation("Blob bucket {Bucket} created.", bucket);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Blob bucket {Bucket} could not be ensured — storage not ready or not permitted.", bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
