using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.CareRegistry;

namespace Kadari.Infrastructure.Persistence.Configurations.CareRegistry;

// The Care registry: platform reference data, separate from customer data and never tenant-scoped (CARE/12 §3, §14).
// References between registry rows are soft (converted, indexed), like the rest of the schema.

public sealed class SourceEvidenceConfiguration : IEntityTypeConfiguration<SourceEvidence>
{
    public void Configure(EntityTypeBuilder<SourceEvidence> builder)
    {
        builder.ToTable("SourceEvidences");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new SourceEvidenceId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(s => s.Title).HasMaxLength(SourceEvidence.MaxTitleLength).IsRequired();
        builder.Property(s => s.Url).HasMaxLength(SourceEvidence.MaxUrlLength);
        builder.Property(s => s.RetrievedAt);
    }
}

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, value => new BrandId(value))
            .ValueGeneratedNever();

        builder.Property(b => b.Name).HasMaxLength(Brand.MaxNameLength).IsRequired();
        builder.Property(b => b.Slug).HasMaxLength(Brand.MaxNameLength).IsRequired();
        builder.HasIndex(b => b.Slug).IsUnique();

        builder.Property(b => b.ParentGroup).HasMaxLength(Brand.MaxTextLength);
        builder.Property(b => b.LocalOperator).HasMaxLength(Brand.MaxTextLength);
        builder.Property(b => b.Categories).HasMaxLength(Brand.MaxTextLength);
        builder.Property(b => b.ServiceModel).HasMaxLength(Brand.MaxTextLength);
        builder.Property(b => b.RegistrationSignal).HasMaxLength(Brand.MaxTextLength);
        builder.Property(b => b.Priority).HasMaxLength(8);
        builder.Property(b => b.ResearchConfidence).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(b => b.SourceEvidenceId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (SourceEvidenceId?)null : new SourceEvidenceId(value.Value));
    }
}

public sealed class RetailerConfiguration : IEntityTypeConfiguration<Retailer>
{
    public void Configure(EntityTypeBuilder<Retailer> builder)
    {
        builder.ToTable("Retailers");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RetailerId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(Retailer.MaxNameLength).IsRequired();
        builder.Property(r => r.Slug).HasMaxLength(Retailer.MaxNameLength).IsRequired();
        builder.Property(r => r.CountryCode).HasColumnType("char(2)").IsRequired();
        builder.HasIndex(r => new { r.CountryCode, r.Slug }).IsUnique();

        builder.Property(r => r.GroupOwner).HasMaxLength(Retailer.MaxTextLength);
        builder.Property(r => r.RetailFormat).HasMaxLength(Retailer.MaxTextLength);
        builder.Property(r => r.Categories).HasMaxLength(Retailer.MaxTextLength);
        builder.Property(r => r.IntegrationStance).HasMaxLength(Retailer.MaxTextLength);
        builder.Property(r => r.Priority).HasMaxLength(8);
        builder.Property(r => r.SourceEvidenceId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (SourceEvidenceId?)null : new SourceEvidenceId(value.Value));
    }
}

public sealed class RoutingModelConfiguration : IEntityTypeConfiguration<RoutingModel>
{
    public void Configure(EntityTypeBuilder<RoutingModel> builder)
    {
        builder.ToTable("RoutingModels");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new RoutingModelId(value))
            .ValueGeneratedNever();

        builder.Property(r => r.Code).HasMaxLength(8).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.WhenItApplies).HasMaxLength(RoutingModel.MaxTextLength);
        builder.Property(r => r.DefaultAction).HasMaxLength(RoutingModel.MaxTextLength);
        builder.Property(r => r.TypicalEvidence).HasMaxLength(RoutingModel.MaxTextLength);
        builder.Property(r => r.Examples).HasMaxLength(RoutingModel.MaxTextLength);
    }
}

public sealed class CoverageRuleVersionConfiguration : IEntityTypeConfiguration<CoverageRuleVersion>
{
    public void Configure(EntityTypeBuilder<CoverageRuleVersion> builder)
    {
        builder.ToTable("CoverageRuleVersions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new CoverageRuleVersionId(value))
            .ValueGeneratedNever();

        // A version number is used once per rule.
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

        builder.Property(r => r.CoverageType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.DurationMonths);
        builder.Property(r => r.StartBasis).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(r => r.RegistrationRequired);
        builder.Property(r => r.RegistrationDeadlineDays);
        builder.Property(r => r.ExtensionMonths);
        builder.Property(r => r.PlainLanguageSummary).HasMaxLength(CoverageRuleVersion.MaxSummaryLength).IsRequired();
        builder.Property(r => r.EffectiveFrom);
        builder.Property(r => r.EffectiveTo);
        builder.Property(r => r.Confidence).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.SourceEvidenceId)
            .HasConversion(id => id.Value, value => new SourceEvidenceId(value));
        builder.Property(r => r.LastVerifiedAt);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.PublishedAt);
        builder.Property(r => r.StatusChangedAt);
        builder.Property(r => r.SupersedesVersionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (CoverageRuleVersionId?)null : new CoverageRuleVersionId(value.Value));

        // The engine's lookup: selectable rules for a country and brand.
        builder.HasIndex(r => new { r.CountryCode, r.Status, r.BrandId });

        builder.Ignore(r => r.Specificity);
        builder.Ignore(r => r.IsSelectable);
        builder.Ignore(r => r.DomainEvents);
    }
}
