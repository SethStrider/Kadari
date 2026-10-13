using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Assets;
using Kadari.Domain.Identity;

namespace Kadari.Infrastructure.Persistence.Configurations.Assets;

public sealed class InsurerUserConfiguration : IEntityTypeConfiguration<InsurerUser>
{
    public void Configure(EntityTypeBuilder<InsurerUser> builder)
    {
        builder.ToTable("InsurerUsers");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new InsurerUserId(value))
            .ValueGeneratedNever();

        // One binding per account — the grant path upserts rather than duplicating.
        builder.Property(u => u.UserAccountId)
            .HasConversion(id => id.Value, value => new UserAccountId(value));
        builder.HasIndex(u => u.UserAccountId).IsUnique();

        builder.Property(u => u.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.HasIndex(u => u.TenantId);

        builder.Property(u => u.GrantedAt);

        builder.Ignore(u => u.DomainEvents);
    }
}
