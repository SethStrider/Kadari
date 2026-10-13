namespace Kadari.Domain.Abstractions;

/// <summary>
/// An aggregate root: the consistency boundary and the only place domain events originate.
/// Handlers orchestrate; invariants and the events they produce live here (CLAUDE.md §5).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }

    protected AggregateRoot() { }

    /// <summary>Events raised since the aggregate was loaded, in the order they occurred.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Called by the dispatch layer once events have been published.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
