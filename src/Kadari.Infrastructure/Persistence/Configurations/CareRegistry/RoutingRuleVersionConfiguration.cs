using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.CareRegistry;

namespace Kadari.Infrastructure.Persistence.Configurations.CareRegistry;

public sealed class RoutingRuleVersionConfiguration : IEntityTypeConfiguration<RoutingRuleVersion>
{
    public void Configure(EntityTypeBuilder<RoutingRuleVersion> builder)
    {
        builder.ToTable("RoutingRuleVersions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RoutingRuleVersionId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.LogicalRuleId);
        builder.Property(r => r.Version);
        builder.HasIndex(r => new { r.LogicalRuleId, r.Version }).IsUnique();

        builder.Property(r => r.CountryCode).HasColumnType("char(2)").IsRequired();
        builder.Property(r => r.BrandId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (BrandId?)null : new BrandId(value.Value));
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.ModelCode).HasMaxLength(CoverageRuleVersion.MaxModelLength);
        builder.Property(r => r.RetailerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (RetailerId?)null : new RetailerId(value.Value));
        builder.Property(r => r.Coverage).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.IssueClass).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.RouteType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.RoutingModelCode).HasMaxLength(8);
        builder.Property(r => r.TargetName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ChannelType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.ChannelValue).HasMaxLength(500);

        // The evidence a route asks for, as names, ';'-separated — a small fixed set read whole.
        builder.Property(r => r.RequiredEvidence)
            .HasConversion(
                items => string.Join(';', items.Select(e => e.ToString())),
                text => text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<RouteEvidence>).ToArray(),
                new ValueComparer<RouteEvidence[]>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, e) => HashCode.Combine(h, e)), v => v.ToArray()))
            .HasMaxLength(300);

        builder.Property(r => r.Instructions).HasMaxLength(RoutingRuleVersion.MaxTextLength * 2).IsRequired();
        builder.Property(r => r.Priority);
        builder.Property(r => r.Confidence).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.SourceEvidenceId).HasConversion(id => id.Value, value => new SourceEvidenceId(value));
        builder.Property(r => r.LastVerifiedAt);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.PublishedAt);
        builder.Property(r => r.StatusChangedAt);

        builder.HasIndex(r => new { r.CountryCode, r.Status, r.BrandId });
        builder.Ignore(r => r.Specificity);
        builder.Ignore(r => r.IsSelectable);
        builder.Ignore(r => r.DomainEvents);
    }
}
