namespace Kadari.Domain.Abstractions;

/// <summary>
/// Marker for something that happened in the domain, raised by an aggregate and dispatched
/// after <c>SaveChanges</c> (then to the bus via the outbox).
/// <para>
/// Deliberately framework-free — no MediatR <c>INotification</c> here. The Application/Infrastructure
/// layers adapt these to the dispatch mechanism; the Domain stays dependency-free (CLAUDE.md §5).
/// </para>
/// </summary>
public interface IDomainEvent;
