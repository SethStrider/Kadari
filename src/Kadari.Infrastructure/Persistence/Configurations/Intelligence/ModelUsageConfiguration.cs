using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Customers;
using Kadari.Domain.Intelligence;

namespace Kadari.Infrastructure.Persistence.Configurations.Intelligence;

/// <summary>Model usage: append-only — no soft-delete, no concurrency token, never updated.</summary>
public sealed class ModelUsageConfiguration : IEntityTypeConfiguration<ModelUsage>
{
    public void Configure(EntityTypeBuilder<ModelUsage> builder)
    {
        builder.ToTable("ModelUsages");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new ModelUsageId(value))
            .ValueGeneratedNever();

        builder.Property(u => u.Feature).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Provider).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Model).HasMaxLength(120).IsRequired();
        builder.Property(u => u.CustomerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (CustomerId?)null : new CustomerId(value.Value));
        builder.Property(u => u.Succeeded);
        builder.Property(u => u.InputTokens);
        builder.Property(u => u.OutputTokens);
        builder.Property(u => u.CostMicros);
        builder.Property(u => u.Currency).HasColumnType("char(3)");
        builder.Property(u => u.OccurredAt);

        // Spend reports: by day and feature, and per customer.
        builder.HasIndex(u => new { u.OccurredAt, u.Feature });
        builder.HasIndex(u => u.CustomerId);
    }
}
