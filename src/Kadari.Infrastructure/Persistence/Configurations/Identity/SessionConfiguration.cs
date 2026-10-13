using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kadari.Domain.Identity;

namespace Kadari.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new SessionId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.UserAccountId)
            .HasConversion(id => id.Value, value => new UserAccountId(value));

        builder.Property(s => s.RefreshTokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(s => s.RefreshTokenHash);
        builder.Property(s => s.PreviousRefreshTokenHash).HasMaxLength(128);
        builder.HasIndex(s => s.PreviousRefreshTokenHash);
        builder.Property(s => s.LoginAt);
        builder.Property(s => s.LastActivityAt);
        builder.Property(s => s.ExpiresAt);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.Device).HasMaxLength(256);
        builder.Property(s => s.IpAddress).HasMaxLength(45);

        builder.HasIndex(s => s.UserAccountId);

        builder.Ignore(s => s.DomainEvents);
    }
}
