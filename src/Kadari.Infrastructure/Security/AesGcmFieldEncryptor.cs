using System.Security.Cryptography;
using System.Text;

namespace Kadari.Infrastructure.Security;

/// <summary>Encrypts/decrypts a sensitive field value at rest.</summary>
public interface IFieldEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

/// <summary>
/// AES-256-GCM field encryption. The key is derived (SHA-256) from the configured secret, so any secret
/// length yields a valid 256-bit key. Output is base64(nonce | tag | ciphertext) with a fresh random nonce
/// per call — the same input encrypts differently each time, which is fine because the field is never queried
/// by value (identity matching uses the subject's HMAC key, not the raw ID).
/// </summary>
public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmFieldEncryptor(string secret)
        => _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret ?? string.Empty));

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);

        var input = Convert.FromBase64String(ciphertext);
        var nonce = input.AsSpan(0, NonceSize);
        var tag = input.AsSpan(NonceSize, TagSize);
        var cipher = input.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
