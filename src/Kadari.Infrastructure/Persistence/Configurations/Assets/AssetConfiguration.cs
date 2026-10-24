using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Owners;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AssetId(value))
            .ValueGeneratedNever();

        // Scoping keys — soft references (converted, indexed), not hard FK constraints (cf. Referral).
        builder.Property(a => a.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(a => a.OwnerId)
            .HasConversion(id => id.Value, value => new OwnerId(value));

        builder.Property(a => a.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.RoomLabel).HasMaxLength(80);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(a => a.RegisteredAt);

        builder.HasIndex(a => a.OwnerId);
        builder.HasIndex(a => a.TenantId);

        // AssetEvidence is a child of the aggregate — reached only through Asset.Evidence (a backing field),
        // keyed to its parent by a shadow FK.
        builder.HasMany(a => a.Evidence)
            .WithOne()
            .HasForeignKey("AssetId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Evidence).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Household co-owners — a child collection reached only through Asset.CoOwners (a backing field).
        builder.HasMany(a => a.CoOwners)
            .WithOne()
            .HasForeignKey("AssetId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.CoOwners).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class AssetCoOwnerConfiguration : IEntityTypeConfiguration<AssetCoOwner>
{
    public void Configure(EntityTypeBuilder<AssetCoOwner> builder)
    {
        builder.ToTable("AssetCoOwners");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new AssetCoOwnerId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.MemberId)
            .HasConversion(id => id.Value, value => new OwnerId(value));
        builder.HasIndex(c => c.MemberId);   // the ledger scans co-owned assets by member

        builder.Property(c => c.AddedAt);
    }
}

public sealed class AssetEvidenceConfiguration : IEntityTypeConfiguration<AssetEvidence>
{
    public void Configure(EntityTypeBuilder<AssetEvidence> builder)
    {
        builder.ToTable("AssetEvidences");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => new AssetEvidenceId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(e => e.Strength).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(80).IsRequired();
        builder.Property(e => e.AssertedAt);
        builder.Property(e => e.Confidence);
        builder.Property(e => e.Reference).HasMaxLength(512);
        builder.Property(e => e.RetentionUntil);   // null = canonical (kept); set = ephemeral (swept)
        builder.Property(e => e.PurgedAt);
        builder.HasIndex(e => e.RetentionUntil);   // the retention sweep scans by this

        // Purchase evidence carries the owner's purchase facts (KADARI Care); null for every other kind.
        builder.OwnsOne(e => e.Purchase, p =>
        {
            p.Property(x => x.Brand).HasColumnName("PurchaseBrand").HasMaxLength(PurchaseFacts.MaxTextLength);
            p.Property(x => x.Model).HasColumnName("PurchaseModel").HasMaxLength(PurchaseFacts.MaxTextLength);
            p.Property(x => x.Retailer).HasColumnName("PurchaseRetailer").HasMaxLength(PurchaseFacts.MaxTextLength);
            p.Property(x => x.PurchasedOn).HasColumnName("PurchasedOn");
        });

        // Value evidence carries a Money amount; null for every other kind (optional owned type).
        builder.OwnsOne(e => e.Amount, m =>
        {
            m.Property(x => x.AmountMinor).HasColumnName("AmountMinor");
            m.Property(x => x.Currency).HasColumnName("AmountCurrency").HasMaxLength(3);
        });
    }
}
