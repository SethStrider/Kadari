using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Kadari.Application.Abstractions.Security;
using Kadari.Application.Abstractions.Storage;

namespace Kadari.Infrastructure.Storage;

/// <summary>
/// Envelope-encrypts every object before it reaches the bucket (AS-07). KADARI photos are a room-by-room inventory of
/// a home's valuables, and uploaded documents carry ID numbers; anyone who can read the bucket or a backup of it
/// would otherwise see them. Each object gets its own random AES-256-GCM data key; that key is wrapped by the field
/// key ring (<see cref="ISensitiveDataProtector"/>), so it names its key id and follows GL-24 rotation.
/// <para>
/// Stored layout: <c>magic | u16 wrapped-key length | wrapped key (UTF-8) | nonce | tag | ciphertext</c>. An object
/// without the magic was written before this and is returned as stored, so existing files keep reading.
/// </para>
/// </summary>
public sealed class EnvelopeEncryptedBlobStorage(IBlobStorage inner, ISensitiveDataProtector keyWrapper) : IBlobStorage
{
    private static readonly byte[] Magic = "TLENV1\0"u8.ToArray();
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var plain = new MemoryStream();
        await content.CopyToAsync(plain, cancellationToken);

        using var sealedObject = new MemoryStream(Seal(plain.GetBuffer().AsSpan(0, (int)plain.Length)));
        await inner.UploadAsync(key, sealedObject, "application/octet-stream", cancellationToken);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        var stored = new MemoryStream();
        await using (var source = await inner.DownloadAsync(key, cancellationToken))
        {
            await source.CopyToAsync(stored, cancellationToken);
        }

        var bytes = stored.GetBuffer().AsSpan(0, (int)stored.Length);
        if (!bytes.StartsWith(Magic))
        {
            stored.Position = 0;
            return stored;   // written before AS-07: plaintext
        }

        var opened = Open(bytes);
        await stored.DisposeAsync();
        return new MemoryStream(opened, writable: false);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => inner.ExistsAsync(key, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => inner.DeleteAsync(key, cancellationToken);

    private byte[] Seal(ReadOnlySpan<byte> plaintext)
    {
        Span<byte> dataKey = stackalloc byte[KeySize];
        RandomNumberGenerator.Fill(dataKey);
        var wrapped = Encoding.UTF8.GetBytes(keyWrapper.Protect(Convert.ToBase64String(dataKey)));

        var output = new byte[Magic.Length + sizeof(ushort) + wrapped.Length + NonceSize + TagSize + plaintext.Length];
        var span = output.AsSpan();
        Magic.CopyTo(span);
        span = span[Magic.Length..];
        BinaryPrimitives.WriteUInt16BigEndian(span, checked((ushort)wrapped.Length));
        span = span[sizeof(ushort)..];
        wrapped.CopyTo(span);
        span = span[wrapped.Length..];

        var nonce = span[..NonceSize];
        var tag = span.Slice(NonceSize, TagSize);
        var ciphertext = span[(NonceSize + TagSize)..];
        RandomNumberGenerator.Fill(nonce);
        using (var aes = new AesGcm(dataKey, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        CryptographicOperations.ZeroMemory(dataKey);
        return output;
    }

    private byte[] Open(ReadOnlySpan<byte> stored)
    {
        var span = stored[Magic.Length..];
        var wrappedLength = BinaryPrimitives.ReadUInt16BigEndian(span);
        span = span[sizeof(ushort)..];
        var dataKey = Convert.FromBase64String(keyWrapper.Unprotect(Encoding.UTF8.GetString(span[..wrappedLength])));
        span = span[wrappedLength..];

        var nonce = span[..NonceSize];
        var tag = span.Slice(NonceSize, TagSize);
        var ciphertext = span[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        return plaintext;
    }
}
