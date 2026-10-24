using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Infrastructure.Persistence.Configurations.Care;

public sealed class CareProfileConfiguration : IEntityTypeConfiguration<CareProfile>
{
    public void Configure(EntityTypeBuilder<CareProfile> builder)
    {
        builder.ToTable("CareProfiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new CareProfileId(value))
            .ValueGeneratedNever();

        // The asset it attaches to — one profile per asset (Care is additive, never a second asset).
        builder.Property(p => p.AssetId)
            .HasConversion(id => id.Value, value => new AssetId(value));
        builder.HasIndex(p => p.AssetId).IsUnique();

        builder.Property(p => p.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.EnabledAt);
        builder.Property(p => p.CoverageState).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(p => p.CoverageExpiresOn);
        builder.Property(p => p.NextAttentionOn);
        builder.Property(p => p.OpenCaseCount);
        builder.Property(p => p.LastEvaluatedAt);
        builder.Property(p => p.LatestDecisionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (CoverageDecisionId?)null : new CoverageDecisionId(value.Value));

        // The reminder sweep will scan by the next attention date.
        builder.HasIndex(p => p.NextAttentionOn);

        builder.HasMany(p => p.Coverage)
            .WithOne()
            .HasForeignKey("CareProfileId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Coverage).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class CoverageRecordConfiguration : IEntityTypeConfiguration<CoverageRecord>
{
    public void Configure(EntityTypeBuilder<CoverageRecord> builder)
    {
        builder.ToTable("CoverageRecords");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new CoverageRecordId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.CoverageType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.State).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.RuleVersionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (CoverageRuleVersionId?)null : new CoverageRuleVersionId(value.Value));
        builder.Property(r => r.StartsOn);
        builder.Property(r => r.ExpiresOn);
        builder.Property(r => r.RegistrationRequired);
        builder.Property(r => r.Confidence).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.PlainLanguageSummary).HasMaxLength(CoverageRuleVersion.MaxSummaryLength);
        builder.Property(r => r.SourceEvidenceId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (SourceEvidenceId?)null : new SourceEvidenceId(value.Value));
        builder.Property(r => r.Explanation).HasMaxLength(CoverageDecision.MaxExplanationLength).IsRequired();
        builder.Property(r => r.DecisionId)
            .HasConversion(id => id.Value, value => new CoverageDecisionId(value));

        // One record per kind of cover on a profile.
        builder.HasIndex("CareProfileId", nameof(CoverageRecord.CoverageType)).IsUnique();
    }
}

/// <summary>Coverage decisions: append-only provenance — no soft-delete, no concurrency token, never updated.</summary>
public sealed class CoverageDecisionConfiguration : IEntityTypeConfiguration<CoverageDecision>
{
    public void Configure(EntityTypeBuilder<CoverageDecision> builder)
    {
        builder.ToTable("CoverageDecisions");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new CoverageDecisionId(value))
            .ValueGeneratedNever();

        builder.Property(d => d.AssetId)
            .HasConversion(id => id.Value, value => new AssetId(value));
        builder.Property(d => d.EvaluatedAt);
        builder.Property(d => d.ResultState).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(d => d.InputSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.MatchedRuleVersionIds).HasColumnType("uuid[]");
        builder.Property(d => d.Explanation).HasMaxLength(CoverageDecision.MaxExplanationLength).IsRequired();
        builder.Property(d => d.EngineVersion).HasMaxLength(32).IsRequired();

        builder.HasIndex(d => new { d.AssetId, d.EvaluatedAt });
    }
}
