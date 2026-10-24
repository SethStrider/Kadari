using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Infrastructure.Persistence;

namespace Kadari.Infrastructure.Messaging;

/// <summary>
/// The bus (CLAUDE.md §6a): MassTransit over RabbitMQ with the EF Core transactional outbox on
/// <see cref="KadariDbContext"/>. Integration events written in a command's transaction are delivered after commit;
/// consumers (in <c>Kadari.Worker</c> only) receive through the EF inbox, so a redelivered message runs once.
/// </summary>
internal static class MessagingExtensions
{
    /// <summary>The Aspire resource / connection-string name of the broker.</summary>
    public const string ConnectionName = "messaging";

    public static void AddMessaging(IHostApplicationBuilder builder, Action<IBusRegistrationConfigurator>? configureConsumers)
    {
        var broker = builder.Configuration.GetConnectionString(ConnectionName);
        var brokerConfigured = !string.IsNullOrWhiteSpace(broker);
        builder.Services.AddSingleton(new MessagingTransport(brokerConfigured));

        builder.Services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddEntityFrameworkOutbox<KadariDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);

                // No broker: keep the messages in the outbox rather than "deliver" them to an in-memory bus with
                // nobody listening. A host with a broker ships them later.
                outbox.UseBusOutbox(delivery =>
                {
                    if (!brokerConfigured)
                    {
                        delivery.DisableDeliveryService();
                    }
                });
            });

            // Every consumer endpoint de-duplicates through the inbox and writes its own published messages through
            // the outbox, in the consumer's transaction.
            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
                endpoint.UseEntityFrameworkOutbox<KadariDbContext>(context));

            configureConsumers?.Invoke(bus);

            if (brokerConfigured)
            {
                bus.UsingRabbitMq((context, rabbit) =>
                {
                    rabbit.Host(new Uri(broker!));
                    rabbit.ConfigureEndpoints(context);
                });
            }
            else
            {
                bus.UsingInMemory((context, memory) => memory.ConfigureEndpoints(context));
            }
        });

        // Replaces the Application's no-op default: the last registration wins.
        builder.Services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();
    }
}
