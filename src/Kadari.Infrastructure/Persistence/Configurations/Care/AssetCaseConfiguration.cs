using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Infrastructure.Persistence.Configurations.Care;

public sealed class AssetCaseConfiguration : IEntityTypeConfiguration<AssetCase>
{
    public void Configure(EntityTypeBuilder<AssetCase> builder)
    {
        builder.ToTable("AssetCases");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new AssetCaseId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.AssetId).HasConversion(id => id.Value, value => new AssetId(value));
        builder.Property(c => c.CaseType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(AssetCase.MaxTitleLength).IsRequired();
        builder.Property(c => c.IssueSummary).HasMaxLength(AssetCase.MaxSummaryLength).IsRequired();
        builder.Property(c => c.IssueClass).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.SafetyFlagged);
        builder.Property(c => c.CoverageAtOpen).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.RoutingDecisionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (RoutingDecisionId?)null : new RoutingDecisionId(value.Value));
        builder.Property(c => c.RouteType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.RouteConfidence).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(c => c.RouteAccepted);
        builder.Property(c => c.TargetName).HasMaxLength(200);
        builder.Property(c => c.ChannelType).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.ChannelValue).HasMaxLength(500);
        builder.Property(c => c.Instructions).HasMaxLength(1000);
        builder.Property(c => c.RequiredEvidence)
            .HasConversion(
                items => string.Join(';', items.Select(e => e.ToString())),
                text => text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<RouteEvidence>).ToArray(),
                new ValueComparer<RouteEvidence[]>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, e) => HashCode.Combine(h, e)), v => v.ToArray()))
            .HasMaxLength(300);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.OpenedAt);
        builder.Property(c => c.NextActionOn);
        builder.Property(c => c.ClosedAt);
        builder.Property(c => c.Outcome).HasConversion<string>().HasMaxLength(24);
        builder.Property(c => c.OutcomeNotes).HasMaxLength(AssetCase.MaxNotesLength);

        builder.HasIndex(c => new { c.AssetId, c.Status });
        // The Care Ops queue: open cases that still need a route.
        builder.HasIndex(c => new { c.RouteType, c.Status });

        builder.OwnsMany(c => c.Events, e =>
        {
            e.ToTable("CaseEvents");
            e.WithOwner().HasForeignKey("AssetCaseId");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(id => id.Value, value => new CaseEventId(value)).ValueGeneratedNever();
            e.Property(x => x.Sequence);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.Actor).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(280).IsRequired();
            e.Property(x => x.OccurredAt);
        });
        builder.Navigation(c => c.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(c => c.Messages, m =>
        {
            m.ToTable("CaseMessages");
            m.WithOwner().HasForeignKey(x => x.CaseId);
            m.HasKey(x => x.Id);
            m.Property(x => x.Id).HasConversion(id => id.Value, value => new CaseMessageId(value)).ValueGeneratedNever();
            m.Property(x => x.CaseId).HasConversion(id => id.Value, value => new AssetCaseId(value));
            m.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16).IsRequired();
            m.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            m.Property(x => x.Body).HasMaxLength(CaseMessage.MaxBodyLength).IsRequired();
            m.Property(x => x.IdempotencyKey).HasMaxLength(100);
            m.Property(x => x.At);
            // A handoff is recorded once per key, even under concurrent retries.
            m.HasIndex(x => new { x.CaseId, x.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        });
        builder.Navigation(c => c.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.References).WithOne().HasForeignKey("AssetCaseId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.References).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(c => c.Commitments).WithOne().HasForeignKey("AssetCaseId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Commitments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(c => c.Appointments).WithOne().HasForeignKey("AssetCaseId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Appointments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.IsClosed);
        builder.Ignore(c => c.NeedsRoute);
        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class CaseReferenceConfiguration : IEntityTypeConfiguration<CaseReference>
{
    public void Configure(EntityTypeBuilder<CaseReference> builder)
    {
        builder.ToTable("CaseReferences");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, value => new CaseReferenceId(value)).ValueGeneratedNever();
        builder.Property(r => r.Party).HasMaxLength(CaseReference.MaxLength).IsRequired();
        builder.Property(r => r.Reference).HasMaxLength(CaseReference.MaxLength).IsRequired();
        builder.Property(r => r.AddedAt);
    }
}

public sealed class CaseCommitmentConfiguration : IEntityTypeConfiguration<CaseCommitment>
{
    public void Configure(EntityTypeBuilder<CaseCommitment> builder)
    {
        builder.ToTable("CaseCommitments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, value => new CaseCommitmentId(value)).ValueGeneratedNever();
        builder.Property(c => c.Party).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(CaseCommitment.MaxDescriptionLength).IsRequired();
        builder.Property(c => c.DueOn);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.SettledAt);
        builder.HasIndex(c => new { c.Status, c.DueOn });
    }
}

public sealed class CaseAppointmentConfiguration : IEntityTypeConfiguration<CaseAppointment>
{
    public void Configure(EntityTypeBuilder<CaseAppointment> builder)
    {
        builder.ToTable("CaseAppointments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, value => new CaseAppointmentId(value)).ValueGeneratedNever();
        builder.Property(a => a.StartsAt);
        builder.Property(a => a.EndsAt);
        builder.Property(a => a.Location).HasMaxLength(CaseAppointment.MaxTextLength);
        builder.Property(a => a.Instructions).HasMaxLength(CaseAppointment.MaxTextLength);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.SettledAt);
    }
}
