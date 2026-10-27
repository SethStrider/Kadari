using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Kadari.Application;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Infrastructure.Messaging;
using Kadari.Infrastructure.Persistence;

namespace Kadari.Infrastructure.Tests.Messaging;

/// <summary>
/// The outbox contract behind <see cref="IIntegrationEventPublisher"/> (CLAUDE.md §6a), against real Postgres and the
/// real Worker composition: an event published in a unit of work is stored with the caller's message id when the
/// work commits, and is not stored at all when it rolls back — so an external action can never run for a command
/// that did not happen.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class IntegrationEventOutboxTests(PostgresFixture fixture) : InfraTest(fixture)
{
    public sealed record ProbeIntegrationEvent(Guid SubjectId);

    private ServiceProvider BuildWorkerServices()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:kadaridb"] = ConnectionString,
            // No broker: outbox delivery stays off, so the rows the test asserts on are left in place.
        });
        builder.Services.AddApplication();
        builder.AddWorkerInfrastructure(_ => { });
        return builder.Services.BuildServiceProvider();
    }

    private async Task<int> OutboxRowsFor(Guid messageId)
    {
        await using var db = new KadariDbContext(
            new DbContextOptionsBuilder<KadariDbContext>().UseNpgsql(ConnectionString).Options);
        return await db.Set<OutboxMessage>().CountAsync(m => m.MessageId == messageId);
    }

    [Fact]
    public async Task Publish_WhenUnitOfWorkCommits_StoresTheMessageUnderTheCallersId()
    {
        await using var services = BuildWorkerServices();
        var messageId = Guid.CreateVersion7();

        await using (var scope = services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var db = scope.ServiceProvider.GetRequiredService<IKadariDbContext>();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await publisher.PublishAsync(new ProbeIntegrationEvent(Guid.CreateVersion7()), messageId, ct);
                await db.SaveChangesAsync(ct);
            });
        }

        Assert.Equal(1, await OutboxRowsFor(messageId));
    }

    [Fact]
    public async Task Publish_WhenUnitOfWorkRollsBack_StoresNothing()
    {
        await using var services = BuildWorkerServices();
        var messageId = Guid.CreateVersion7();

        await using (var scope = services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var db = scope.ServiceProvider.GetRequiredService<IKadariDbContext>();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                {
                    await publisher.PublishAsync(new ProbeIntegrationEvent(Guid.CreateVersion7()), messageId, ct);
                    await db.SaveChangesAsync(ct);
                    throw new InvalidOperationException("the command failed after publishing");
                }));
        }

        Assert.Equal(0, await OutboxRowsFor(messageId));
    }

    [Fact]
    public void Composition_WithoutABroker_ReportsItSoTheReadinessGuardCanRefuse()
    {
        using var services = BuildWorkerServices();

        Assert.False(services.GetRequiredService<MessagingTransport>().BrokerConfigured);
    }
}
