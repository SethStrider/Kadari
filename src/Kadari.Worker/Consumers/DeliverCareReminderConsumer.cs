using MassTransit;
using MediatR;
using Kadari.Application.Abstractions.Notifications;
using Kadari.Application.Features.Care.DeliverCareReminder;
using Kadari.Contracts.Care;

namespace Kadari.Worker.Consumers;

/// <summary>
/// Delivers a due Care reminder (CARE/14 C6): asks the Application who to tell and what to say, sends it through the
/// single notification seam, and records the outcome. Received through the EF inbox, so a redelivery of the same
/// message is skipped; the reminder itself refuses to be marked sent twice. A send that throws is retried by the
/// endpoint's retry policy and the reminder stays queued until it succeeds.
/// </summary>
public sealed class DeliverCareReminderConsumer(ISender sender, INotificationChannel channel) : IConsumer<CareReminderDue>
{
    public async Task Consume(ConsumeContext<CareReminderDue> context)
    {
        var ct = context.CancellationToken;
        var delivery = await sender.Send(new GetReminderDeliveryQuery(context.Message.ReminderId), ct);

        if (!delivery.Send)
        {
            await sender.Send(new RecordReminderDeliveryCommand(context.Message.ReminderId, Sent: false, Channel: null), ct);
            return;
        }

        await channel.SendAsync(delivery.Channel!.Value, delivery.Recipient!, delivery.Body!, containsSecret: false, ct);
        await sender.Send(new RecordReminderDeliveryCommand(context.Message.ReminderId, Sent: true, delivery.Channel.ToString()), ct);
    }
}
