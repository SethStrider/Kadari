namespace Kadari.Domain.Abstractions;

/// <summary>
/// Non-generic base for every persistent domain object: the audit stamps and soft-delete flags
/// (DB standards / CLAUDE.md §6). The identity lives on <see cref="Entity{TId}"/>; keeping these
/// fields on a non-generic base lets a single interceptor apply them across all entity types.
/// <para>Audit stamps and soft-delete are populated centrally by an interceptor — never by callers.</para>
/// </summary>
public abstract class Entity
{
    /// <summary>Set once, on first persist, by the audit interceptor.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set on every subsequent persist by the audit interceptor.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Soft-delete marker. A "deleted" row is filtered out, never removed.</summary>
    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Marks this entity soft-deleted. Idempotent.</summary>
    public void MarkDeleted(DateTimeOffset when)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = when;
    }
}
