using MassTransit;
using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Infrastructure.Messaging;

/// <summary>
/// The outbox-backed <see cref="IIntegrationEventPublisher"/>. The scoped <see cref="IPublishEndpoint"/> is
/// MassTransit's EF bus outbox: publishing adds an <c>OutboxMessage</c> row to the same <c>KadariDbContext</c>, so the
/// message commits or rolls back with the command that produced it, and the outbox delivery service ships it to the
/// broker only after commit. The caller's <paramref name="messageId"/> becomes the MassTransit message id, which the
/// Worker's inbox uses to drop a redelivery.
/// </summary>
internal sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, Guid messageId, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return publishEndpoint.Publish(integrationEvent, context => context.MessageId = messageId, cancellationToken);
    }
}
