using Kadari.Application.Common.Files;

namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>
/// An uploaded still, held on disk rather than on the heap (ported from PoziSnap). Buffering the request
/// body into a <c>byte[]</c> is what makes an upload endpoint spike — every concurrent request pins its
/// whole image in memory (LOH past 85 KB). Spilling to a temp file keeps steady-state allocation flat,
/// and the bytes are only pulled into memory when something actually needs them.
/// <para>Disposing deletes the file, so the owner must dispose even on the failure path.</para>
/// </summary>
public sealed class SnapImage : IAsyncDisposable
{
    private readonly string _path;
    private int _disposed;

    public SnapImage(string path, string contentType, long length)
    {
        _path = path;
        ContentType = contentType;
        Length = length;
    }

    public string ContentType { get; }

    public long Length { get; }

    /// <summary>Opens the still for sequential async reading.</summary>
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
        // Delete once, and never let cleanup take down the request.
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
