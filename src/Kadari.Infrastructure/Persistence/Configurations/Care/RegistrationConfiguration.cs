using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Infrastructure.Persistence.Configurations.Care;

public sealed class RegistrationRuleVersionConfiguration : IEntityTypeConfiguration<RegistrationRuleVersion>
{
    public void Configure(EntityTypeBuilder<RegistrationRuleVersion> builder)
    {
        builder.ToTable("RegistrationRuleVersions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RegistrationRuleVersionId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.LogicalRuleId);
        builder.Property(r => r.Version);
        builder.HasIndex(r => new { r.LogicalRuleId, r.Version }).IsUnique();

        builder.Property(r => r.CountryCode).HasColumnType("char(2)").IsRequired();
        builder.Property(r => r.BrandId).HasConversion(id => id.Value, value => new BrandId(value));
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.ModelCode).HasMaxLength(CoverageRuleVersion.MaxModelLength);
        builder.Property(r => r.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.DestinationType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Destination).HasMaxLength(500).IsRequired();

        // The required fields as names, ';'-separated — a small fixed set read whole.
        builder.Property(r => r.RequiredFields)
            .HasConversion(
                fields => string.Join(';', fields.Select(f => f.ToString())),
                text => text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<RegistrationField>).ToArray(),
                new ValueComparer<RegistrationField[]>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, f) => HashCode.Combine(h, f)), v => v.ToArray()))
            .HasMaxLength(300);

        builder.Property(r => r.DeadlineDays);
        builder.Property(r => r.ExtensionMonths);
        builder.Property(r => r.BenefitDescription).HasMaxLength(RegistrationRuleVersion.MaxTextLength).IsRequired();
        builder.Property(r => r.Instructions).HasMaxLength(RegistrationRuleVersion.MaxTextLength * 2);
        builder.Property(r => r.Confidence).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.SourceEvidenceId).HasConversion(id => id.Value, value => new SourceEvidenceId(value));
        builder.Property(r => r.LastVerifiedAt);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.PublishedAt);
        builder.Property(r => r.StatusChangedAt);

        builder.HasIndex(r => new { r.CountryCode, r.Status, r.BrandId });
        builder.Ignore(r => r.IsSelectable);
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class RegistrationOpportunityConfiguration : IEntityTypeConfiguration<RegistrationOpportunity>
{
    public void Configure(EntityTypeBuilder<RegistrationOpportunity> builder)
    {
        builder.ToTable("RegistrationOpportunities");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new RegistrationOpportunityId(value))
            .ValueGeneratedNever();

        builder.Property(o => o.AssetId).HasConversion(id => id.Value, value => new AssetId(value));
        builder.Property(o => o.LogicalRuleId);
        // One opportunity per asset and rule.
        builder.HasIndex(o => new { o.AssetId, o.LogicalRuleId }).IsUnique();

        builder.Property(o => o.RuleVersionId).HasConversion(id => id.Value, value => new RegistrationRuleVersionId(value));
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(o => o.BenefitSummary).HasMaxLength(RegistrationRuleVersion.MaxTextLength).IsRequired();
        builder.Property(o => o.DeadlineOn);
        builder.Property(o => o.SurfacedAt);
        builder.Property(o => o.ResolvedAt);

        // The reminder sweep looks for open opportunities by deadline.
        builder.HasIndex(o => new { o.Status, o.DeadlineOn });
        builder.Ignore(o => o.DomainEvents);
    }
}

public sealed class CoverageRegistrationConfiguration : IEntityTypeConfiguration<CoverageRegistration>
{
    public void Configure(EntityTypeBuilder<CoverageRegistration> builder)
    {
        builder.ToTable("CoverageRegistrations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new CoverageRegistrationId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.AssetId).HasConversion(id => id.Value, value => new AssetId(value));
        builder.HasIndex(r => r.AssetId);
        builder.Property(r => r.OpportunityId).HasConversion(id => id.Value, value => new RegistrationOpportunityId(value));
        builder.Property(r => r.RuleVersionId).HasConversion(id => id.Value, value => new RegistrationRuleVersionId(value));
        builder.Property(r => r.AuthorisationId).HasConversion(id => id.Value, value => new ExternalActionAuthorisationId(value));
        builder.Property(r => r.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Destination).HasMaxLength(500).IsRequired();

        // A retried request can never start a second registration (CARE/12 §12).
        builder.Property(r => r.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.IdempotencyKey).IsUnique();

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.StartedAt);
        builder.Property(r => r.ExternalReference).HasMaxLength(CoverageRegistration.MaxReferenceLength);
        builder.Property(r => r.RegisteredOn);
        builder.Property(r => r.CompletedAt);
        builder.Property(r => r.FailedAt);
        builder.Property(r => r.FailureReason).HasMaxLength(CoverageRegistration.MaxReasonLength);

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class ExternalActionAuthorisationConfiguration : IEntityTypeConfiguration<ExternalActionAuthorisation>
{
    public void Configure(EntityTypeBuilder<ExternalActionAuthorisation> builder)
    {
        builder.ToTable("ExternalActionAuthorisations");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new ExternalActionAuthorisationId(value))
            .ValueGeneratedNever();

        builder.Property(a => a.GrantedByAccountId);
        builder.Property(a => a.AssetId).HasConversion(id => id.Value, value => new AssetId(value));
        builder.HasIndex(a => a.AssetId);
        builder.Property(a => a.ActionType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.ScopeJson).HasColumnType("jsonb").IsRequired();
        builder.Property(a => a.TermsVersion).HasMaxLength(64).IsRequired();
        builder.Property(a => a.GrantedAt);
        builder.Property(a => a.RevokedAt);

        builder.Ignore(a => a.DomainEvents);
    }
}

/// <summary>External-action audit: append-only — no soft-delete, no concurrency token, never updated.</summary>
public sealed class ExternalActionAuditConfiguration : IEntityTypeConfiguration<ExternalActionAudit>
{
    public void Configure(EntityTypeBuilder<ExternalActionAudit> builder)
    {
        builder.ToTable("ExternalActionAudits");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new ExternalActionAuditId(value))
            .ValueGeneratedNever();

        builder.Property(a => a.AssetId).HasConversion(id => id.Value, value => new AssetId(value));
        builder.Property(a => a.CaseId);
        builder.Property(a => a.ActionType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.AuthorisationId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (ExternalActionAuthorisationId?)null : new ExternalActionAuthorisationId(value.Value));
        builder.Property(a => a.Actor).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.ActorId);
        builder.Property(a => a.Destination).HasMaxLength(ExternalActionAudit.MaxTextLength).IsRequired();
        builder.Property(a => a.Step).HasMaxLength(ExternalActionAudit.MaxTextLength).IsRequired();
        builder.Property(a => a.SharedSummary).HasMaxLength(ExternalActionAudit.MaxTextLength);
        builder.Property(a => a.Result).HasMaxLength(ExternalActionAudit.MaxTextLength);
        builder.Property(a => a.ExternalReference).HasMaxLength(ExternalActionAudit.MaxTextLength);
        builder.Property(a => a.OccurredAt);

        builder.HasIndex(a => new { a.AssetId, a.OccurredAt });
    }
}
