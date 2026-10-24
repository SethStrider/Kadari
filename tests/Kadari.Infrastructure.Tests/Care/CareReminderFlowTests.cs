using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using Kadari.Application;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Application.Features.Assets.RecordPurchaseDetails;
using Kadari.Application.Features.Care.DeliverCareReminder;
using Kadari.Application.Features.Care.DispatchDueCareReminders;
using Kadari.Application.Features.Care.EnableCare;
using Kadari.Application.Features.Care.EvaluateCare;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.Customers;
using Kadari.Domain.Notifications;
using Kadari.Worker.Consumers;
using static Kadari.Infrastructure.Tests.Care.CareTestHost;

namespace Kadari.Infrastructure.Tests.Care;

/// <summary>
/// Reminders against real Postgres, and — once — end to end through a real RabbitMQ (CARE/14 C6): evaluation schedules
/// them without duplicates, the sweep queues due ones through the outbox exactly once, delivery respects the owner's
/// opt-in, and a queued reminder travels outbox → broker → the Worker's consumer → "sent".
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CareReminderFlowTests(PostgresFixture fixture) : InfraTest(fixture)
{
    private sealed class RecordingChannel : INotificationChannel
    {
        public List<(NotificationChannel Channel, string Recipient, string Body)> Sent { get; } = [];

        public Task SendAsync(NotificationChannel channel, string recipient, string body, bool containsSecret = false, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add((channel, recipient, body));
            }

            return Task.CompletedTask;
        }
    }

    private async Task<T> SendAsync<T>(MediatR.IRequest<T> request)
    {
        await using var db = NewContext(ConnectionString);
        return await Sender(db).Send(request);
    }

    /// <summary>An asset with confirmed cover; its 90-day reminder is made due today.</summary>
    private async Task<(Guid Customer, Guid Asset)> ArrangeDueReminderAsync(string mobile, bool optedIn)
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule()));
        var (customer, asset) = await SeedAssetAsync(ConnectionString, mobile);
        await AttachReceiptAsync(ConnectionString, asset);
        await SendAsync(new RecordPurchaseDetailsCommand(customer, asset, "Bosch Home", null, null, DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-7))));
        await SendAsync(new EnableCareCommand(customer, asset, "MajorAppliance"));

        await using var db = NewContext(ConnectionString);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE kadari.\"CareReminders\" SET \"DueOn\" = CURRENT_DATE WHERE \"Type\" = 'Warranty90'");
        if (optedIn)
        {
            db.KadariConsents.Add(KadariConsent.Grant(new CustomerId(customer), KadariConsentScope.CareReminders, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        return (customer, asset);
    }

    [Fact]
    public async Task Evaluation_SchedulesTheExpiryRemindersOnce()
    {
        await ImportAsync(ConnectionString, Snapshot(BoschRule()));
        var (customer, asset) = await SeedAssetAsync(ConnectionString, "+27820004001");
        await AttachReceiptAsync(ConnectionString, asset);
        await SendAsync(new RecordPurchaseDetailsCommand(customer, asset, "Bosch Home", null, null, DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-7))));
        await SendAsync(new EnableCareCommand(customer, asset, "MajorAppliance"));

        await SendAsync(new EvaluateCareCommand(customer, asset));
        await SendAsync(new EvaluateCareCommand(customer, asset));

        await using var db = NewContext(ConnectionString);
        var types = await db.Set<CareReminder>().OrderBy(r => r.DueOn).Select(r => r.Type).ToListAsync();
        Assert.Equal([CareReminderType.Warranty90, CareReminderType.Warranty30], types);
    }

    [Fact]
    public async Task Sweep_QueuesADueReminderThroughTheOutboxExactlyOnce()
    {
        await ArrangeDueReminderAsync("+27820004002", optedIn: true);

        // The real composition (outbox publisher), without a broker: messages stay in the outbox table to be counted.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:kadaridb"] = ConnectionString });
        builder.Services.AddApplication();
        builder.AddWorkerInfrastructure(_ => { });
        await using var services = builder.Services.BuildServiceProvider();

        async Task<DispatchDueCareRemindersResponse> SweepAsync()
        {
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(new DispatchDueCareRemindersCommand());
        }

        var first = await SweepAsync();
        var second = await SweepAsync();

        await using var db = NewContext(ConnectionString);
        Assert.Equal((1, 0, 1), (first.Queued, second.Queued, await db.Set<OutboxMessage>().CountAsync()));
    }

    [Fact]
    public async Task Delivery_WithoutOptIn_SendsNothingAndLeavesItInTheApp()
    {
        await ArrangeDueReminderAsync("+27820004003", optedIn: false);
        await SendAsync(new DispatchDueCareRemindersCommand());
        Guid reminderId;
        await using (var db = NewContext(ConnectionString))
        {
            reminderId = (await db.Set<CareReminder>().SingleAsync(r => r.Status == CareReminderStatus.Queued)).Id.Value;
        }

        var delivery = await SendAsync(new GetReminderDeliveryQuery(reminderId));

        Assert.Equal((false, "Not opted in"), (delivery.Send, delivery.SkipReason));
    }

    [Fact]
    public async Task AQueuedReminder_TravelsThroughRabbitMqToTheWorkerAndIsSentOnce()
    {
        await ArrangeDueReminderAsync("+27820004004", optedIn: true);
        await using var rabbit = new RabbitMqBuilder().WithImage("rabbitmq:3.11").Build();
        await rabbit.StartAsync();

        var channel = new RecordingChannel();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:kadaridb"] = ConnectionString,
            ["ConnectionStrings:messaging"] = rabbit.GetConnectionString(),
            ["Jwt:SigningKey"] = "kadari-local-dev-signing-key-change-me-32b!!!",
        });
        builder.Services.AddApplication();
        builder.AddWorkerInfrastructure(bus => bus.AddConsumer<DeliverCareReminderConsumer>());
        builder.Services.AddSingleton<INotificationChannel>(channel);
        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            await using (var scope = host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(new DispatchDueCareRemindersCommand());
            }

            // Outbox delivery polls every second; the consumer then records the send. Bounded wait, no fixed sleep.
            var deadline = DateTime.UtcNow.AddSeconds(45);
            CareReminder? reminder = null;
            while (DateTime.UtcNow < deadline)
            {
                await using var db = NewContext(ConnectionString);
                reminder = await db.Set<CareReminder>().AsNoTracking().SingleAsync(r => r.Type == CareReminderType.Warranty90);
                if (reminder.Status == CareReminderStatus.Sent)
                {
                    break;
                }

                await Task.Delay(250);
            }

            Assert.Equal(
                (CareReminderStatus.Sent, 1, true),
                (reminder!.Status, channel.Sent.Count, channel.Sent[0].Body.Contains("warranty ends on")));
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
