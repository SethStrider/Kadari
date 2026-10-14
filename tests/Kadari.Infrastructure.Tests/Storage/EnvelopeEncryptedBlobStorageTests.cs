using System.Text;
using Kadari.Application.Abstractions.Storage;
using Kadari.Infrastructure.Security;
using Kadari.Infrastructure.Storage;

namespace Kadari.Infrastructure.Tests.Storage;

/// <summary>AS-07: the bucket only ever holds ciphertext, and files written before encryption still read.</summary>
public class EnvelopeEncryptedBlobStorageTests
{
    private sealed class InMemoryBlobStorage : IBlobStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = new();

        public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            Objects[key] = ms.ToArray();
        }

        public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Objects[key]));
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(Objects.ContainsKey(key));
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.FromResult(Objects.Remove(key));
    }

    private readonly InMemoryBlobStorage _bucket = new();
    private readonly EnvelopeEncryptedBlobStorage _storage;

    public EnvelopeEncryptedBlobStorageTests() =>
        _storage = new EnvelopeEncryptedBlobStorage(_bucket, new FieldProtectorSensitiveDataProtector());

    private static async Task<string> ReadAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task An_uploaded_photo_is_stored_encrypted_and_reads_back_unchanged()
    {
        const string photo = "JFIF... gold watch serial 12345 in the main bedroom";

        await _storage.UploadAsync("kadari/c/snap.img", new MemoryStream(Encoding.UTF8.GetBytes(photo)), "image/jpeg");

        Assert.DoesNotContain("serial 12345", Encoding.UTF8.GetString(_bucket.Objects["kadari/c/snap.img"]));
        Assert.Equal(photo, await ReadAsync(await _storage.DownloadAsync("kadari/c/snap.img")));
    }

    [Fact]
    public async Task A_file_written_before_encryption_still_reads()
    {
        _bucket.Objects["documents/old.pdf"] = Encoding.UTF8.GetBytes("%PDF-1.7 legacy");

        Assert.Equal("%PDF-1.7 legacy", await ReadAsync(await _storage.DownloadAsync("documents/old.pdf")));
    }
}
