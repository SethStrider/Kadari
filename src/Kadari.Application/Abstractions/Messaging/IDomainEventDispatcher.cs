using Kadari.Domain.Abstractions;

namespace Kadari.Application.Abstractions.Messaging;

/// <summary>Publishes domain events raised by aggregates to their in-process handlers.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
