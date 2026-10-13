namespace Kadari.Domain.Abstractions;

/// <summary>
/// Implemented by aggregate roots so the dispatch layer can collect and clear domain events
/// without knowing the aggregate's key type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
