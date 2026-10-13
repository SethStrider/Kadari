using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Identity;

namespace Kadari.Infrastructure.Persistence.Configurations;

public sealed class PendingExternalLoginConfiguration : IEntityTypeConfiguration<PendingExternalLogin>
{
    public void Configure(EntityTypeBuilder<PendingExternalLogin> builder)
    {
        builder.ToTable("PendingExternalLogins");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new PendingExternalLoginId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Subject).HasMaxLength(256).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(320);
        builder.Property(p => p.EmailVerified).IsRequired();
        builder.Property(p => p.IssuedAt).IsRequired();
        builder.Property(p => p.ExpiresAt).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.CompletedAt);

        builder.Property(p => p.UserAccountId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (UserAccountId?)null : new UserAccountId(value.Value));

        // No FK to UserAccounts: while pending there is no account yet, and that is the whole point of
        // the table. The column is the audit trail of what the sign-in eventually resolved to.

        // The sweep that retires abandoned sign-ins, and the lookup during completion.
        builder.HasIndex(p => new { p.Status, p.ExpiresAt });
        builder.HasIndex(p => new { p.Provider, p.Subject });

        builder.Ignore(p => p.DomainEvents);
    }
}
