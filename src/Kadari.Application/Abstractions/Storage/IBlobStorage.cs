namespace Kadari.Application.Abstractions.Storage;

/// <summary>
/// Abstraction over blob storage (S3/MinIO) so the app never depends on the AWS SDK directly
/// (CLAUDE.md §6). Implemented by <c>S3BlobStorage</c> in Infrastructure.
/// </summary>
public interface IBlobStorage
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
