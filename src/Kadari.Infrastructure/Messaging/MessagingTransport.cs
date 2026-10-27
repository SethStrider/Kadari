namespace Kadari.Infrastructure.Messaging;

/// <summary>
/// Which transport the bus came up on. With a broker (<c>ConnectionStrings:messaging</c>) the outbox delivers to
/// RabbitMQ; without one the bus is in-memory and outbox delivery is switched off, so integration events wait in the
/// outbox table instead of being dropped. Only Development may run like that; the production readiness guard refuses
/// a host without a broker.
/// </summary>
public sealed record MessagingTransport(bool BrokerConfigured);
