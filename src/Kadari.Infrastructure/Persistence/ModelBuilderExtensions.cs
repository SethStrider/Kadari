using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Kadari.Domain.Abstractions;

namespace Kadari.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds a global query filter <c>e =&gt; !e.IsDeleted</c> to every mapped entity that derives from
    /// <see cref="Entity"/>, so soft-deleted rows are invisible to normal queries (DB standards).
    /// Use <c>IgnoreQueryFilters()</c> to see them (e.g. for retention/admin).
    /// Call after entity configurations have been applied.
    /// </summary>
    public static void ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(Entity.IsDeleted));
            var notDeleted = Expression.Not(isDeleted);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(notDeleted, parameter));
        }
    }

    /// <summary>
    /// Makes every <see cref="Entity"/> use Postgres' <c>xmin</c> system column as its optimistic
    /// concurrency token (DB standards) — no extra rowversion column. Npgsql-specific; KadariDbContext
    /// always runs on Postgres.
    /// </summary>
    public static void ApplyXminConcurrency(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // The manual equivalent of Npgsql's UseXminAsConcurrencyToken() — works on the
            // non-generic builder we get from modelBuilder.Entity(Type).
            modelBuilder.Entity(entityType.ClrType)
                .Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
