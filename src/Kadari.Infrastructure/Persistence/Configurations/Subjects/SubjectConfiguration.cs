using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Customers;
using Kadari.Domain.Subjects;
using Kadari.Infrastructure.Persistence.Converters;

namespace Kadari.Infrastructure.Persistence.Configurations.Subjects;

/// <summary>
/// Maps the <see cref="Subject"/> aggregate (DES-001b). Identifiers are <c>jsonb</c> (read as a whole with
/// the subject). The nullable <c>CustomerId</c> link is indexed so "the subject for this customer" is a
/// cheap lookup (the Customer→Subject bridge).
/// </summary>
public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    private static ValueComparer<IReadOnlyDictionary<string, string>> DictComparer() => new(
        (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
        v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
        v => v.ToDictionary(kv => kv.Key, kv => kv.Value));

    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new SubjectId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.DisplayName).HasMaxLength(256).IsRequired();

        builder.Property(s => s.CustomerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (CustomerId?)null : new CustomerId(value.Value));
        builder.HasIndex(s => s.CustomerId);

        // Identifiers can hold sensitive values (a third-party subject's national ID), so the bag is encrypted
        // at rest (AES-GCM). The column is text — encrypted ciphertext, not jsonb; the bag is read whole.
        builder.Property(s => s.Identifiers)
            .HasConversion(new EncryptedStringDictionaryConverter(), DictComparer())
            .HasColumnName("Identifiers");

        // Non-reversible identity key (hex HMAC-SHA256 = 64 chars); indexed so a claim can match subjects by it.
        builder.Property(s => s.IdentityKeyHash).HasMaxLength(64);
        builder.HasIndex(s => s.IdentityKeyHash);

        builder.Ignore(s => s.DomainEvents);
    }
}
