using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

public sealed class AssetPassportConfiguration : IEntityTypeConfiguration<AssetPassport>
{
    public void Configure(EntityTypeBuilder<AssetPassport> builder)
    {
        builder.ToTable("AssetPassports");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new AssetPassportId(value))
            .ValueGeneratedNever();

        // The owning key — one passport per asset.
        builder.Property(p => p.AssetId)
            .HasConversion(id => id.Value, value => new AssetId(value));
        builder.HasIndex(p => p.AssetId).IsUnique();

        // Scoping keys — soft references (converted, indexed), not hard FK constraints (cf. Asset).
        builder.Property(p => p.OwnerId)
            .HasConversion(id => id.Value, value => new OwnerId(value));
        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.HasIndex(p => p.OwnerId);
        builder.HasIndex(p => p.TenantId);

        builder.Property(p => p.VerificationId).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.VerificationId).IsUnique();

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Assurance).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(p => p.EvidenceCount);
        builder.Property(p => p.IssuedAt);
        builder.Property(p => p.LastUpdatedAt);

        // The snapshotted best replacement value; null when none was recorded (optional owned type).
        builder.OwnsOne(p => p.ReplacementValue, m =>
        {
            m.Property(x => x.AmountMinor).HasColumnName("ReplacementValueMinor");
            m.Property(x => x.Currency).HasColumnName("ReplacementValueCurrency").HasMaxLength(3);
        });

        builder.Ignore(p => p.DomainEvents);
    }
}
