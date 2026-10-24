using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;

namespace Kadari.Infrastructure.Persistence.Configurations.Care;

public sealed class CareReminderConfiguration : IEntityTypeConfiguration<CareReminder>
{
    public void Configure(EntityTypeBuilder<CareReminder> builder)
    {
        builder.ToTable("CareReminders");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new CareReminderId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.AssetId).HasConversion(id => id.Value, value => new AssetId(value));
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.DueOn);
        builder.Property(r => r.SourceType).HasMaxLength(64).IsRequired();
        builder.Property(r => r.SourceId);

        // Creation is idempotent: the same reminder for the same thing on the same day exists once (CARE/12 §6).
        builder.Property(r => r.DedupeKey).HasMaxLength(160).IsRequired();
        builder.HasIndex(r => r.DedupeKey).IsUnique();

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.SnoozedUntil);
        builder.Property(r => r.QueuedAt);
        builder.Property(r => r.DeliveredAt);
        builder.Property(r => r.DeliveredVia).HasMaxLength(16);
        builder.Property(r => r.ClosedAt);

        // The sweep scans by status and date; the app reads an asset's reminders.
        builder.HasIndex(r => new { r.Status, r.DueOn });
        builder.HasIndex(r => r.AssetId);
        builder.Ignore(r => r.DomainEvents);
    }
}
