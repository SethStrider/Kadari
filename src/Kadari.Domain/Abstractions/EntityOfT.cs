namespace Kadari.Domain.Abstractions;

/// <summary>
/// A persistent entity with a strongly-typed key (e.g. <c>CustomerId</c>) — prevents passing the
/// wrong id type at compile time (CLAUDE.md §5). Keys are UUIDv7, assigned by the domain at
/// construction (DB standards).
/// </summary>
public abstract class Entity<TId> : Entity
    where TId : struct
{
    protected Entity(TId id) => Id = id;

    /// <summary>Required by EF Core materialisation only.</summary>
    protected Entity() { }

    public TId Id { get; protected init; }
}
