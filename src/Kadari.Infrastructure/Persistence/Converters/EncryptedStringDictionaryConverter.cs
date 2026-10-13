using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Persistence.Converters;

/// <summary>
/// Serialises a string dictionary to JSON and encrypts it at rest (AES-GCM) through the process-wide
/// <see cref="FieldProtector"/> — for identifier bags that may hold sensitive values (a national ID, etc.).
/// On read it decrypts then deserialises; a stored value that is not ciphertext (legacy plaintext JSON such as
/// <c>{}</c>) is tolerated and parsed as-is, so switching a column from plaintext jsonb to encrypted text is
/// backward-compatible. The column becomes text (ciphertext is not valid jsonb); the bag is read whole, never
/// queried by value, so nothing depended on jsonb querying.
/// </summary>
public sealed class EncryptedStringDictionaryConverter : ValueConverter<IReadOnlyDictionary<string, string>, string>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public EncryptedStringDictionaryConverter()
        : base(v => FieldProtector.Encrypt(JsonSerializer.Serialize(v, Json)), v => Deserialize(v))
    {
    }

    private static IReadOnlyDictionary<string, string> Deserialize(string stored)
    {
        string json;
        try
        {
            json = FieldProtector.Decrypt(stored);
        }
        catch
        {
            json = stored;   // legacy plaintext written before encryption — parse as-is
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Json) ?? new Dictionary<string, string>();
    }
}
