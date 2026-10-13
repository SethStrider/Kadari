namespace Kadari.Application.Abstractions.Messaging;

/// <summary>
/// Publishes integration events (from <c>Kadari.Contracts</c>) to the bus via the transactional
/// outbox, keeping MassTransit out of the Application layer (as <see cref="IDomainEventDispatcher"/>
/// does for domain events). The event is buffered on the next <c>SaveChangesAsync</c> of the same
/// scoped <c>IKadariDbContext</c> — publish and save must share the scope, then the outbox delivery
/// service ships it. A caller-supplied <paramref name="messageId"/> lets a repeated source produce the
/// same message id, so the consumer-side inbox can de-duplicate.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, Guid messageId, CancellationToken cancellationToken = default)
        where TEvent : class;
}
