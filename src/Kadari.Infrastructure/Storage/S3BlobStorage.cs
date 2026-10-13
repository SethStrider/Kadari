using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Storage;

namespace Kadari.Infrastructure.Storage;

/// <summary>
/// <see cref="IBlobStorage"/> backed by the AWS S3 SDK. Works against MinIO locally and real S3
/// in production — only the <see cref="S3Options"/> differ.
/// </summary>
public sealed class S3BlobStorage(IAmazonS3 client, IOptions<S3Options> options) : IBlobStorage
{
    private readonly S3Options _options = options.Value;

    // Unsigned payloads (DisablePayloadSigning) let the SDK interoperate with MinIO, but the AWS SDK only
    // permits them over HTTPS — the hash isn't protecting the body, so the transport must. Real S3 (no custom
    // ServiceUrl) and an HTTPS MinIO qualify; a plain-HTTP MinIO (local dev) does not, so we sign there instead.
    private readonly bool _disablePayloadSigning = IsHttps(options.Value.ServiceUrl);

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = _disablePayloadSigning,
        };

        await client.PutObjectAsync(request, cancellationToken);
    }

    // Real S3 (empty ServiceUrl) defaults to HTTPS; a custom endpoint is HTTPS only when its scheme says so.
    private static bool IsHttps(string? serviceUrl) =>
        string.IsNullOrWhiteSpace(serviceUrl)
        || serviceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public async Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await client.GetObjectAsync(_options.BucketName, key, cancellationToken);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetObjectMetadataAsync(_options.BucketName, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => client.DeleteObjectAsync(_options.BucketName, key, cancellationToken);
}
