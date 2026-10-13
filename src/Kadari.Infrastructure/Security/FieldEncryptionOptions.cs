namespace Kadari.Infrastructure.Security;

/// <summary>
/// The keys behind at-rest field encryption (AES-GCM). <see cref="Key"/> encrypts everything new under
/// <see cref="KeyId"/>; <see cref="PreviousKeys"/> only decrypt (GL-24).
/// <para>
/// To rotate: set <c>KeyId</c> to a new id and <c>Key</c> to the new secret, move the old secret into
/// <c>PreviousKeys:{oldId}</c>, deploy, then run the migrations job with <c>--reencrypt-fields</c>. Keep the old key
/// in <c>PreviousKeys</c> until encrypted blobs written under it (raw provider payloads) have aged out.
/// </para>
/// </summary>
public sealed class FieldEncryptionOptions
{
    public const string SectionName = "FieldEncryption";

    /// <summary>The current secret: encrypts every new value.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The current key's id, written into every new ciphertext. Letters, digits, '-' and '_'.</summary>
    public string KeyId { get; set; } = "1";

    /// <summary>Retired secrets by id: decrypt only, never encrypt.</summary>
    public Dictionary<string, string> PreviousKeys { get; set; } = [];

    /// <summary>
    /// One retired key as a fixed pair, for environments that cannot name a dictionary entry from a variable
    /// (docker compose). Merged into <see cref="PreviousKeys"/> when both are set.
    /// </summary>
    public string RetiredKeyId { get; set; } = string.Empty;

    public string RetiredKey { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> AllPreviousKeys()
    {
        var all = new Dictionary<string, string>(PreviousKeys, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(RetiredKeyId) && !string.IsNullOrWhiteSpace(RetiredKey))
        {
            all[RetiredKeyId] = RetiredKey;
        }

        return all;
    }
}
