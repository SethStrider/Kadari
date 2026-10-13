using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Identity;

namespace Kadari.Infrastructure.Persistence.Configurations;

public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogins");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new ExternalLoginId(value))
            .ValueGeneratedNever();

        builder.Property(l => l.UserAccountId)
            .HasConversion(id => id.Value, value => new UserAccountId(value));

        builder.Property(l => l.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(l => l.Subject).HasMaxLength(256).IsRequired();
        builder.Property(l => l.Email).HasMaxLength(320);
        builder.Property(l => l.EmailVerified).IsRequired();
        builder.Property(l => l.LinkedAt).IsRequired();
        builder.Property(l => l.RevokedAt);

        // One live credential per provider subject, platform-wide — otherwise the same Google account
        // could sign into two Trustli accounts. Partial, so a revoked row stays for audit and does not
        // block the same subject being linked again later.
        builder.HasIndex(l => new { l.Provider, l.Subject })
            .IsUnique()
            .HasFilter("\"RevokedAt\" IS NULL");

        // The sign-in lookup: provider + subject → account.
        builder.HasIndex(l => l.UserAccountId);
    }
}
