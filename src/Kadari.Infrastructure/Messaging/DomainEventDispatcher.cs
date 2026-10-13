using MediatR;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Domain.Abstractions;

namespace Kadari.Infrastructure.Messaging;

/// <summary>Publishes each domain event as a <see cref="DomainEventNotification{T}"/> via MediatR.</summary>
public sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
            await publisher.Publish(notification, cancellationToken);
        }
    }
}
