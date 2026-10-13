using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Optional white-label branding (deck §27–28) — columns are null when the tenant uses the platform palette.
        builder.OwnsOne(t => t.Branding, b =>
        {
            b.Property(x => x.PrimaryColor).HasColumnName("BrandingPrimaryColor").HasMaxLength(7);
            b.Property(x => x.AccentColor).HasColumnName("BrandingAccentColor").HasMaxLength(7);
            b.Property(x => x.LogoReference).HasColumnName("BrandingLogoReference").HasMaxLength(512);
        });

        builder.Ignore(t => t.DomainEvents);
    }
}
