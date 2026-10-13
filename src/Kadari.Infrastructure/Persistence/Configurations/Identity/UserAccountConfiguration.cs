using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Access;
using Kadari.Domain.Customers;
using Kadari.Domain.Identity;

namespace Kadari.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccounts");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new UserAccountId(value))
            .ValueGeneratedNever();

        // Required: the verified mobile is what identifies a human, and no account exists without one —
        // a first-time social sign-in parks in PendingExternalLogins until the OTP supplies it. NOT NULL
        // here is what makes that rule structural rather than a convention handlers have to remember.
        builder.Property(u => u.MobileNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(u => u.MobileNumber).IsUnique();

        // Unique for the same reason: the primary email identifies the account to a human, and two
        // accounts claiming one address is the ambiguity that account-linking exists to avoid.
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FirstName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Country).HasMaxLength(2).IsRequired();
        builder.Property(u => u.PreferredLanguage).HasMaxLength(16);
        builder.Property(u => u.TimeZone).HasMaxLength(64);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(u => u.LastLoginAt);

        // Password credential + brute-force state (nullable — OTP/social-only accounts never set one).
        // The stored hash is the self-describing PBKDF2 string, comfortably under 256 chars.
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.FailedLoginAttempts).IsRequired();
        builder.Property(u => u.LockedOutUntil);

        builder.Property(u => u.CustomerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value.Value,
                value => value == null ? (CustomerId?)null : new CustomerId(value.Value));

        // External logins are part of the UserAccount aggregate — the "at least one active credential"
        // invariant spans the mobile number and this collection, so they share a consistency boundary.
        builder.HasMany(u => u.ExternalLogins)
            .WithOne()
            .HasForeignKey(l => l.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.ExternalLogins).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Roles: additive set stored as a comma-separated text column.
        var rolesComparer = new ValueComparer<IReadOnlyList<Role>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, role) => HashCode.Combine(hash, role.GetHashCode())),
            v => v.ToList());

        builder.Property(u => u.Roles)
            .HasConversion(
                roles => string.Join(',', roles.Select(r => r.ToString())),
                text => text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(Enum.Parse<Role>)
                            .ToList(),
                rolesComparer)
            .HasColumnName("Roles")
            .HasMaxLength(200)
            .IsRequired();

        builder.Ignore(u => u.DomainEvents);
    }
}
