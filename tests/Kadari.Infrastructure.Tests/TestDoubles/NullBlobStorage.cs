using Kadari.Application.Abstractions.Storage;

namespace Kadari.Infrastructure.Tests;

/// <summary>
/// A do-nothing <see cref="IBlobStorage"/> for tests whose flow resolves a handler that depends on blob
/// storage but never exercises a media path. Uploads are discarded; a download of a stored key returns its
/// bytes, anything else an empty stream. Tests that assert on stored content use their own capturing fake.
/// </summary>
internal sealed class NullBlobStorage : IBlobStorage
{
    private readonly Dictionary<string, byte[]> _objects = new();

    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _objects[key] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(new MemoryStream(_objects.TryGetValue(key, out var b) ? b : []));

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_objects.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.Remove(key);
        return Task.CompletedTask;
    }
}
