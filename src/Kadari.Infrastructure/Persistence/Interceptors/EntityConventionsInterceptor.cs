using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Kadari.Domain.Abstractions;

namespace Kadari.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Applies the base-entity conventions on every save (DB standards / CLAUDE.md §6):
/// <list type="bullet">
///   <item>stamps <c>CreatedAt</c> on insert and <c>UpdatedAt</c> on update (via <see cref="TimeProvider"/>);</item>
///   <item>turns a hard <c>Remove()</c> into a soft-delete — the app never physically deletes.</item>
/// </list>
/// </summary>
public sealed class EntityConventionsInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Deleted:
                    // Never physically delete: flip to an update that marks the row deleted.
                    entry.State = EntityState.Modified;
                    entry.Entity.MarkDeleted(now);
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
