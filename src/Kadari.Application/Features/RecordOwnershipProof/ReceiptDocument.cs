using Kadari.Application.Common.Files;

namespace Kadari.Application.Features.RecordOwnershipProof;

/// <summary>
/// An uploaded proof-of-ownership document (a receipt, invoice or valuation), held on disk rather than the
/// heap for the same reason as a captured still: buffering the whole upload into a <c>byte[]</c> pins it in
/// memory (LOH past 85 KB) under concurrency. The bytes are pulled in only when the blob store reads them.
/// <para>Disposing deletes the file, so the owner must dispose even on the failure path.</para>
/// </summary>
public sealed class ReceiptDocument : IAsyncDisposable
{
    private readonly string _path;
    private int _disposed;

    public ReceiptDocument(string path, string contentType, long length)
    {
        _path = path;
        ContentType = contentType;
        Length = length;
    }

    public string ContentType { get; }

    public long Length { get; }

    /// <summary>Opens the document for sequential async reading.</summary>
    public Stream OpenRead() => new FileStream(
        _path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 64 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <summary>Removes location and camera metadata from the file before anything reads or stores it (AS-06).
    /// A no-op for anything that is not a JPEG, PNG or WebP.</summary>
    public Task StripMetadataAsync(CancellationToken ct) => ImageMetadata.StripFileInPlaceAsync(_path, ct);

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                File.Delete(_path);
            }
            catch (IOException)
            {
                // Left for the OS temp sweeper.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return ValueTask.CompletedTask;
    }
}
