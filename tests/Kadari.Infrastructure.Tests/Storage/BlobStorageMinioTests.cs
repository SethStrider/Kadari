using System.Text;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Kadari.Infrastructure.Storage;

namespace Kadari.Infrastructure.Tests.Storage;

/// <summary>
/// Blob-storage tests against a real MinIO container over plain HTTP — the two paths that previously had no
/// automated cover (they only surfaced in a live Aspire run):
/// <list type="bullet">
/// <item><see cref="S3BlobStorage"/>'s HTTP payload-signing rule (1ec25be) — an unsigned payload is rejected
/// by the AWS SDK over HTTP, so uploads must sign there; the fix made this scheme-conditional.</item>
/// <item><see cref="BlobStorageInitializer"/>'s bucket-ensure at boot (f864423).</item>
/// </list>
/// </summary>
[Collection(MinioCollection.Name)]
public sealed class BlobStorageMinioTests(MinioFixture minio)
{
    private static async Task EnsureBucketAsync(Amazon.S3.IAmazonS3 client, string bucket)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(client, bucket))
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        }
    }

    [Fact]
    public async Task Upload_and_download_round_trip_over_plain_http_minio()
    {
        var options = minio.OptionsForNewBucket();
        using var client = minio.CreateClient(options);
        await EnsureBucketAsync(client, options.BucketName);

        var storage = new S3BlobStorage(client, Options.Create(options));
        var payload = Encoding.UTF8.GetBytes("kadari canonical still");

        // Before 1ec25be this threw over HTTP (DisablePayloadSigning was unconditional); now it signs and MinIO accepts.
        await storage.UploadAsync("kadari/roundtrip.img", new MemoryStream(payload), "image/jpeg");

        await using var downloaded = await storage.DownloadAsync("kadari/roundtrip.img");
        using var buffer = new MemoryStream();
        await downloaded.CopyToAsync(buffer);

        Assert.Equal(payload, buffer.ToArray());
    }

    [Fact]
    public async Task Exists_reflects_upload_then_delete()
    {
        var options = minio.OptionsForNewBucket();
        using var client = minio.CreateClient(options);
        await EnsureBucketAsync(client, options.BucketName);
        var storage = new S3BlobStorage(client, Options.Create(options));

        Assert.False(await storage.ExistsAsync("k"));

        await storage.UploadAsync("k", new MemoryStream([1, 2, 3]), "application/octet-stream");
        Assert.True(await storage.ExistsAsync("k"));

        await storage.DeleteAsync("k");
        Assert.False(await storage.ExistsAsync("k"));
    }

    [Fact]
    public async Task Initializer_creates_the_bucket_when_missing()
    {
        var options = minio.OptionsForNewBucket();
        using var client = minio.CreateClient(options);
        Assert.False(await AmazonS3Util.DoesS3BucketExistV2Async(client, options.BucketName));

        var initializer = new BlobStorageInitializer(
            client, Options.Create(options), NullLogger<BlobStorageInitializer>.Instance);
        await initializer.StartAsync(CancellationToken.None);

        Assert.True(await AmazonS3Util.DoesS3BucketExistV2Async(client, options.BucketName));
    }

    [Fact]
    public async Task Initializer_is_idempotent_when_the_bucket_already_exists()
    {
        var options = minio.OptionsForNewBucket();
        using var client = minio.CreateClient(options);
        await client.PutBucketAsync(new PutBucketRequest { BucketName = options.BucketName });

        var initializer = new BlobStorageInitializer(
            client, Options.Create(options), NullLogger<BlobStorageInitializer>.Instance);

        // A second ensure must be a no-op, never throwing on the already-present bucket.
        await initializer.StartAsync(CancellationToken.None);

        Assert.True(await AmazonS3Util.DoesS3BucketExistV2Async(client, options.BucketName));
    }
}
