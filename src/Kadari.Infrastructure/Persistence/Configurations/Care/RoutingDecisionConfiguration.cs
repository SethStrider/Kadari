using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Infrastructure.Persistence.Configurations.Care;

/// <summary>Routing decisions: append-only provenance — no soft-delete, no concurrency token, never updated.</summary>
public sealed class RoutingDecisionConfiguration : IEntityTypeConfiguration<RoutingDecision>
{
    public void Configure(EntityTypeBuilder<RoutingDecision> builder)
    {
        builder.ToTable("RoutingDecisions");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new RoutingDecisionId(value))
            .ValueGeneratedNever();

        builder.Property(d => d.AssetId).HasConversion(id => id.Value, value => new AssetId(value));
        builder.Property(d => d.EvaluatedAt);
        builder.Property(d => d.InputSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.MatchedRuleVersionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (RoutingRuleVersionId?)null : new RoutingRuleVersionId(value.Value));
        builder.Property(d => d.RouteType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(d => d.Confidence).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(d => d.Explanation).HasMaxLength(RoutingDecision.MaxExplanationLength).IsRequired();
        builder.Property(d => d.EngineVersion).HasMaxLength(32).IsRequired();
        builder.Property(d => d.HumanOverride);
        builder.Property(d => d.HumanOverrideReason).HasMaxLength(RoutingDecision.MaxExplanationLength);

        builder.HasIndex(d => new { d.AssetId, d.EvaluatedAt });
    }
}
