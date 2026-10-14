using Amazon.S3;
using Testcontainers.Minio;
using Kadari.Infrastructure.Storage;

namespace Kadari.Infrastructure.Tests;

/// <summary>
/// One MinIO container shared by the blob-storage suite — real S3-compatible storage over <b>plain HTTP</b>,
/// like-for-like with local/dev MinIO. Each test uses its own uniquely-named bucket for isolation, so the
/// container starts once for the whole collection. This is what guards the two dev-only blob paths that had
/// no automated cover: the HTTP payload-signing rule in <see cref="S3BlobStorage"/> and the bucket-ensure in
/// <see cref="BlobStorageInitializer"/>.
/// </summary>
public sealed class MinioFixture : IAsyncLifetime
{
    private const string RootUser = "minioadmin";
    private const string RootPassword = "minioadmin";

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("minio/minio:RELEASE.2024-01-16T16-07-38Z")
        .WithUsername(RootUser)
        .WithPassword(RootPassword)
        .Build();

    /// <summary>The container's S3 endpoint — a plain-HTTP URL (e.g. http://127.0.0.1:49xxx).</summary>
    public string ServiceUrl { get; private set; } = null!;

    public string AccessKey => RootUser;

    public string SecretKey => RootPassword;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();
        ServiceUrl = _minio.GetConnectionString();
    }

    public Task DisposeAsync() => _minio.DisposeAsync().AsTask();

    /// <summary>Options for a fresh, uniquely-named bucket on this container (path-style, HTTP endpoint).</summary>
    public S3Options OptionsForNewBucket() => new()
    {
        ServiceUrl = ServiceUrl,
        AccessKey = AccessKey,
        SecretKey = SecretKey,
        Region = "us-east-1",
        BucketName = $"trustli-{Guid.NewGuid():N}",
        ForcePathStyle = true,
    };

    /// <summary>Builds an S3 client against this container exactly as the production DI does.</summary>
    public IAmazonS3 CreateClient(S3Options options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            ServiceURL = options.ServiceUrl,
            AuthenticationRegion = options.Region,
        };

        return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }
}

/// <summary>Shares one <see cref="MinioFixture"/> (a single container) across the blob-storage tests.</summary>
[CollectionDefinition(Name)]
public sealed class MinioCollection : ICollectionFixture<MinioFixture>
{
    public const string Name = "minio";
}
