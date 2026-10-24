using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;

namespace Kadari.Infrastructure.Persistence.Configurations.Care;

public sealed class OpsWorkItemConfiguration : IEntityTypeConfiguration<OpsWorkItem>
{
    public void Configure(EntityTypeBuilder<OpsWorkItem> builder)
    {
        builder.ToTable("OpsWorkItems");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasConversion(id => id.Value, value => new OpsWorkItemId(value)).ValueGeneratedNever();

        builder.Property(w => w.WorkType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(w => w.Severity).HasConversion<string>().HasMaxLength(4).IsRequired();
        builder.Property(w => w.AssetId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (AssetId?)null : new AssetId(value.Value));
        builder.Property(w => w.CaseId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (AssetCaseId?)null : new AssetCaseId(value.Value));
        builder.Property(w => w.ReasonCode).HasMaxLength(64).IsRequired();
        builder.Property(w => w.Summary).HasMaxLength(OpsWorkItem.MaxSummaryLength).IsRequired();

        // The same exception is queued once, however often it is noticed.
        builder.Property(w => w.DedupeKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(w => w.DedupeKey).IsUnique();

        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(w => w.AssignedTo);
        builder.Property(w => w.RaisedAt);
        builder.Property(w => w.DueAt);
        builder.Property(w => w.Resolution).HasConversion<string>().HasMaxLength(24);
        builder.Property(w => w.ResolutionNotes).HasMaxLength(OpsWorkItem.MaxNotesLength);
        builder.Property(w => w.ResolvedBy);
        builder.Property(w => w.ResolvedAt);

        // The queue: open work by severity, then age.
        builder.HasIndex(w => new { w.Status, w.Severity, w.RaisedAt });
        builder.HasIndex(w => w.CaseId);
        builder.Ignore(w => w.DomainEvents);
    }
}
