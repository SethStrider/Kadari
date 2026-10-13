using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Kadari.Infrastructure.Security;

/// <summary>
/// Field encryption that can rotate its key (GL-24). Every ciphertext names the key that wrote it:
/// <c>v2:{keyId}:{base64(nonce | tag | ciphertext)}</c>. Encrypts with the current key; decrypts with whichever key
/// the prefix names. A value written before key ids existed (no prefix) is tried against the current key, then each
/// previous key: AES-GCM authenticates, so a wrong key fails cleanly rather than returning garbage.
/// </summary>
public sealed partial class KeyRingFieldEncryptor : IFieldEncryptor
{
    private const string Prefix = "v2:";

    private readonly string _currentId;
    private readonly AesGcmFieldEncryptor _current;
    private readonly IReadOnlyDictionary<string, AesGcmFieldEncryptor> _keys;

    public KeyRingFieldEncryptor(string currentKeyId, string currentSecret, IReadOnlyDictionary<string, string>? previousSecrets = null)
    {
        ValidateId(currentKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSecret);

        var keys = new Dictionary<string, AesGcmFieldEncryptor>(StringComparer.Ordinal);
        foreach (var (id, secret) in previousSecrets ?? new Dictionary<string, string>())
        {
            ValidateId(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(secret, $"PreviousKeys:{id}");
            keys[id] = new AesGcmFieldEncryptor(secret);
        }

        if (keys.ContainsKey(currentKeyId))
        {
            throw new ArgumentException($"Key id '{currentKeyId}' is the current key and cannot also be a previous key.");
        }

        _currentId = currentKeyId;
        _current = new AesGcmFieldEncryptor(currentSecret);
        keys[currentKeyId] = _current;
        _keys = keys;
    }

    /// <summary>The id new ciphertext is written under.</summary>
    public string CurrentKeyId => _currentId;

    public string Encrypt(string plaintext) => $"{Prefix}{_currentId}:{_current.Encrypt(plaintext)}";

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);

        if (TryParse(ciphertext, out var keyId, out var body))
        {
            return _keys.TryGetValue(keyId, out var key)
                ? key.Decrypt(body)
                : throw new CryptographicException($"No field-encryption key with id '{keyId}' is configured.");
        }

        // Untagged: written before key ids. The current key first, it is the likeliest.
        CryptographicException? last = null;
        foreach (var key in _keys.Values.OrderByDescending(k => ReferenceEquals(k, _current)))
        {
            try
            {
                return key.Decrypt(ciphertext);
            }
            catch (CryptographicException ex)
            {
                last = ex;
            }
        }

        throw last ?? new CryptographicException("No field-encryption key could decrypt the value.");
    }

    /// <summary>Whether <paramref name="ciphertext"/> is already under the current key (re-encryption skips it).</summary>
    public bool IsCurrent(string ciphertext) =>
        TryParse(ciphertext, out var keyId, out _) && keyId == _currentId;

    private static bool TryParse(string ciphertext, out string keyId, out string body)
    {
        keyId = body = string.Empty;
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var separator = ciphertext.IndexOf(':', Prefix.Length);
        if (separator < 0)
        {
            return false;
        }

        keyId = ciphertext[Prefix.Length..separator];
        body = ciphertext[(separator + 1)..];
        return true;
    }

    private static void ValidateId(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId) || !KeyIdPattern().IsMatch(keyId))
        {
            throw new ArgumentException($"Field-encryption key id '{keyId}' must be 1-32 letters, digits, '-' or '_'.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex KeyIdPattern();
}
