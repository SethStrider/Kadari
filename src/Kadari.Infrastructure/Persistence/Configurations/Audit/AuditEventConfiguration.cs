using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Audit;

namespace Kadari.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => new AuditEventId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.ActorKind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.ActorId);
        builder.Property(e => e.SubjectType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.SubjectId);
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PreviousState);
        builder.Property(e => e.NewState);
        builder.Property(e => e.OccurredAt);
        builder.Property(e => e.IpAddress).HasMaxLength(45);

        // Query the log by subject, most recent first.
        builder.HasIndex(e => new { e.SubjectType, e.SubjectId, e.OccurredAt });
    }
}
