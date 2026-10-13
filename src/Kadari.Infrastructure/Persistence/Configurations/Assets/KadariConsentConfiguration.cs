using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

public sealed class KadariConsentConfiguration : IEntityTypeConfiguration<KadariConsent>
{
    public void Configure(EntityTypeBuilder<KadariConsent> builder)
    {
        builder.ToTable("KadariConsents");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new KadariConsentId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));

        builder.Property(c => c.Scope).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.GrantedAt);
        builder.Property(c => c.WithdrawnAt);

        // One consent row per (customer, scope) — the grant path upserts rather than duplicating.
        builder.HasIndex(c => new { c.CustomerId, c.Scope }).IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}
