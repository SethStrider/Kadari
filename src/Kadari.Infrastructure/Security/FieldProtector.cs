using Microsoft.Extensions.Configuration;

namespace Kadari.Infrastructure.Security;

/// <summary>
/// Process-wide field encryptor for EF value converters, which cannot take constructor dependencies. Defaults
/// to a fixed dev key so tests and design-time round-trip without configuration; <c>AddInfrastructure</c>
/// swaps in the real per-environment key ring from <see cref="FieldEncryptionOptions"/> at startup. The keys are
/// process-wide secrets, so a static holder is the pragmatic seam here.
/// </summary>
public static class FieldProtector
{
    /// <summary>The built-in fallback key. Refused outside Development by <see cref="SecretOptionsValidation"/>.</summary>
    public const string DevelopmentKey = "trustli-default-field-encryption-key";

    private static volatile IFieldEncryptor _encryptor = new KeyRingFieldEncryptor("1", DevelopmentKey);

    public static void Configure(IFieldEncryptor encryptor)
        => _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));

    /// <summary>
    /// Builds the key ring from the <c>FieldEncryption</c> section (current key + id, previous keys) and makes it the
    /// process-wide encryptor. With no key configured the development default stands. Every host that reads or
    /// writes encrypted columns calls this: the API and worker through <c>AddInfrastructure</c>, and the migrations
    /// job, which re-encrypts. Returns the ring in use.
    /// </summary>
    public static KeyRingFieldEncryptor ConfigureFrom(IConfiguration configuration)
    {
        var options = configuration.GetSection(FieldEncryptionOptions.SectionName).Get<FieldEncryptionOptions>()
            ?? new FieldEncryptionOptions();
        var ring = string.IsNullOrWhiteSpace(options.Key)
            ? new KeyRingFieldEncryptor("1", DevelopmentKey, options.AllPreviousKeys())
            : new KeyRingFieldEncryptor(options.KeyId, options.Key, options.AllPreviousKeys());
        Configure(ring);
        return ring;
    }

    public static string Encrypt(string plaintext) => _encryptor.Encrypt(plaintext);

    public static string Decrypt(string ciphertext) => _encryptor.Decrypt(ciphertext);
}
