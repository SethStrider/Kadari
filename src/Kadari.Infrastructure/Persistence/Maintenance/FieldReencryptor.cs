using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Kadari.Infrastructure.Persistence.Converters;
using Kadari.Infrastructure.Security;

namespace Kadari.Infrastructure.Persistence.Maintenance;

/// <summary>
/// Moves every encrypted column onto the current field-encryption key (GL-24), so a retired key can eventually be
/// dropped. The columns are found from the EF model (every property mapped through an encrypting converter), so a
/// newly encrypted column is covered without editing this. Works on the stored text directly: decrypt with the key
/// ring, encrypt with the current key, write back. No entity is loaded, so audit timestamps and soft-delete filters
/// do not apply and deleted rows are re-encrypted too. Values already under the current key are skipped, so a re-run
/// is cheap. Run by the migrations job with <c>--reencrypt-fields</c>.
/// </summary>
public sealed class FieldReencryptor(KadariDbContext db, KeyRingFieldEncryptor keys, ILogger logger)
{
    public sealed record ColumnResult(string Table, string Column, int Reencrypted, int Unreadable);

    public async Task<IReadOnlyList<ColumnResult>> ReencryptAsync(CancellationToken ct)
    {
        var results = new List<ColumnResult>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is null)
            {
                continue;
            }

            var table = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
            var primaryKey = entity.FindPrimaryKey()?.Properties;
            if (primaryKey is not { Count: 1 })
            {
                continue;
            }

            foreach (var property in entity.GetDeclaredProperties())
            {
                var isDictionary = property.GetValueConverter() is EncryptedStringDictionaryConverter;
                if (!isDictionary && property.GetValueConverter() is not EncryptedStringConverter)
                {
                    continue;
                }

                results.Add(await ReencryptColumnAsync(
                    entity.GetSchema(), tableName, primaryKey[0].GetColumnName(table)!, property.GetColumnName(table)!,
                    isDictionary, ct));
            }
        }

        return results;
    }

    private async Task<ColumnResult> ReencryptColumnAsync(
        string? schema, string table, string keyColumn, string column, bool isDictionary, CancellationToken ct)
    {
        var qualified = schema is null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(ct);
        }

        var pending = new List<(object Key, string Value)>();
        var unreadable = 0;
        await using (var select = connection.CreateCommand())
        {
            select.CommandText = $"SELECT {Quote(keyColumn)}, {Quote(column)} FROM {qualified} WHERE {Quote(column)} IS NOT NULL";
            await using var reader = await select.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var stored = reader.GetString(1);
                if (keys.IsCurrent(stored))
                {
                    continue;
                }

                if (Reencrypt(stored, isDictionary) is { } reencrypted)
                {
                    pending.Add((reader.GetValue(0), reencrypted));
                }
                else
                {
                    unreadable++;
                }
            }
        }

        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            foreach (var (key, value) in pending)
            {
                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = $"UPDATE {qualified} SET {Quote(column)} = @value WHERE {Quote(keyColumn)} = @key";
                AddParameter(update, "@value", value);
                AddParameter(update, "@key", key);
                await update.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }

        if (unreadable > 0)
        {
            // Never logs values: only where they are. A value no configured key can read stays as it is.
            logger.LogWarning(
                "Field re-encryption: {Count} value(s) in {Table}.{Column} could not be decrypted with any configured key.",
                unreadable, table, column);
        }

        logger.LogInformation("Field re-encryption: {Count} value(s) in {Table}.{Column} moved to key {KeyId}.",
            pending.Count, table, column, keys.CurrentKeyId);
        return new ColumnResult(table, column, pending.Count, unreadable);
    }

    private string? Reencrypt(string stored, bool isDictionary)
    {
        try
        {
            return keys.Encrypt(keys.Decrypt(stored));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // An identifier bag written before encryption existed is plain JSON: encrypt it as it is.
            return isDictionary && stored.TrimStart().StartsWith('{') ? keys.Encrypt(stored) : null;
        }
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
