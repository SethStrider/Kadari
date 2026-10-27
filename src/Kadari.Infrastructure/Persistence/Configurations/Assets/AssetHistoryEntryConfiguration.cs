using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

/// <summary>The asset's permanent history: append-only, no soft-delete, no concurrency token (it is never updated).</summary>
public sealed class AssetHistoryEntryConfiguration : IEntityTypeConfiguration<AssetHistoryEntry>
{
    public void Configure(EntityTypeBuilder<AssetHistoryEntry> builder)
    {
        builder.ToTable("AssetHistoryEntries");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, value => new AssetHistoryEntryId(value))
            .ValueGeneratedNever();

        // Soft reference to the asset (converted, indexed), like the asset's other satellites.
        builder.Property(h => h.AssetId)
            .HasConversion(id => id.Value, value => new AssetId(value));

        builder.Property(h => h.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(h => h.Summary).HasMaxLength(AssetHistoryEntry.MaxSummaryLength).IsRequired();
        builder.Property(h => h.OccurredAt);
        builder.Property(h => h.SourceType).HasMaxLength(64);
        builder.Property(h => h.SourceId);

        // An asset's history, newest first.
        builder.HasIndex(h => new { h.AssetId, h.OccurredAt });
    }
}
