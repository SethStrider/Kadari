using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("TenantMemberships");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new TenantMembershipId(value))
            .ValueGeneratedNever();

        // One membership per customer — the assignment path upserts rather than duplicating.
        builder.Property(m => m.CustomerId)
            .HasConversion(id => id.Value, value => new CustomerId(value));
        builder.HasIndex(m => m.CustomerId).IsUnique();

        builder.Property(m => m.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.HasIndex(m => m.TenantId);

        builder.Property(m => m.AssignedAt);

        builder.Ignore(m => m.DomainEvents);
    }
}
