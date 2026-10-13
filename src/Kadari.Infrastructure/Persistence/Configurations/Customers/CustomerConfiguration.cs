using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Customers;
using Kadari.Infrastructure.Persistence.Converters;

namespace Kadari.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new CustomerId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.FirstName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.MobileNumber).HasMaxLength(32).IsRequired();
        builder.Property(c => c.Country).HasMaxLength(2).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320);
        // National ID — POPIA special personal information. Encrypted at rest (AES-GCM); the column holds
        // base64 ciphertext, so it is widened past the raw 13-digit value.
        builder.Property(c => c.IdentityNumber)
            .HasConversion(new EncryptedStringConverter())
            .HasMaxLength(256);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(c => c.MobileNumber);

        builder.Ignore(c => c.DomainEvents);
    }
}
