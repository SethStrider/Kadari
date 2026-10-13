using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Identity;

namespace Kadari.Infrastructure.Persistence.Configurations;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("OtpChallenges");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new OtpChallengeId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.MobileNumber).HasMaxLength(32).IsRequired();
        builder.Property(c => c.CodeHash).HasMaxLength(128).IsRequired();
        builder.Property(c => c.ExpiresAt);
        builder.Property(c => c.Attempts);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasIndex(c => new { c.MobileNumber, c.Status });

        builder.Ignore(c => c.DomainEvents);
    }
}
