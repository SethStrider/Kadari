namespace Kadari.Infrastructure.Storage;

/// <summary>
/// Configuration for S3-compatible blob storage. Locally this points at MinIO; in production
/// leave <see cref="ServiceUrl"/> empty and supply AWS credentials/region so the AWS SDK talks
/// to real S3. Bound from the "S3" configuration section.
/// </summary>
public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>Custom endpoint (e.g. MinIO). Empty/null => default AWS S3 endpoint.</summary>
    public string? ServiceUrl { get; set; }

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public string? Region { get; set; }

    public string BucketName { get; set; } = "trustli";

    /// <summary>MinIO requires path-style addressing; real S3 works with it too.</summary>
    public bool ForcePathStyle { get; set; } = true;
}
