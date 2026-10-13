using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Customers;
using Kadari.Domain.Assets;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

public sealed class VisionUsageConfiguration : IEntityTypeConfiguration<VisionUsage>
{
    public void Configure(EntityTypeBuilder<VisionUsage> builder)
    {
        builder.ToTable("VisionUsages");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new VisionUsageId(value))
            .ValueGeneratedNever();

        builder.Property(u => u.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));
        builder.Property(u => u.Day).IsRequired();
        builder.Property(u => u.Calls).IsRequired();

        // One counter per customer per day; the day index serves the platform-wide total.
        builder.HasIndex(u => new { u.CustomerId, u.Day }).IsUnique();
        builder.HasIndex(u => u.Day);

        builder.Ignore(u => u.DomainEvents);
    }
}
