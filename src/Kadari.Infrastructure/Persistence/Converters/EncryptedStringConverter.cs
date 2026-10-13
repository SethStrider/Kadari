using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Persistence.Converters;

/// <summary>
/// Encrypts a string column at rest (AES-GCM) through the process-wide <see cref="FieldProtector"/>. EF applies
/// value converters only to non-null values, so null stays null. The column stays a text column — it simply
/// holds ciphertext — so no schema change is needed to adopt it (only enough width for the base64 output).
/// </summary>
public sealed class EncryptedStringConverter() : ValueConverter<string, string>(
    plaintext => FieldProtector.Encrypt(plaintext),
    ciphertext => FieldProtector.Decrypt(ciphertext));
