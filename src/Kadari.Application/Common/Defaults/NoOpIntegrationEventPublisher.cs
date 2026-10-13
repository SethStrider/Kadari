using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Common;

/// <summary>
/// Default <see cref="IIntegrationEventPublisher"/> registered by <c>AddApplication</c>: drops the event.
/// Keeps the Application layer runnable without MassTransit (unit tests, in-memory hosts). Infrastructure
/// registers the real outbox-backed publisher, which wins in the composed app.
/// </summary>
internal sealed class NoOpIntegrationEventPublisher : IIntegrationEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, Guid messageId, CancellationToken cancellationToken = default)
        where TEvent : class
        => Task.CompletedTask;
}
