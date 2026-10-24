using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.DeliverCareReminder;

/// <summary>
/// Record what happened to a queued reminder: sent over a channel, or not sent (no opt-in, no contact) — in which case
/// it waits in the app. Idempotent: a reminder that already left the queue is left as it is.
/// </summary>
/// <remarks>Unscoped: the Worker records the outcome of a delivery it made as the system.</remarks>
public sealed record RecordReminderDeliveryCommand(Guid ReminderId, bool Sent, string? Channel) : ICommand<bool>, IUnscopedRequest;

internal sealed class RecordReminderDeliveryHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<RecordReminderDeliveryCommand, bool>
{
    public async Task<bool> Handle(RecordReminderDeliveryCommand cmd, CancellationToken ct)
    {
        var reminder = await db.Set<CareReminder>().FirstOrDefaultAsync(r => r.Id == new CareReminderId(cmd.ReminderId), ct);
        if (reminder is null)
        {
            return false;
        }

        var changed = cmd.Sent ? reminder.MarkSent(cmd.Channel ?? "unknown", clock.GetUtcNow()) : reminder.MarkNotSent();
        await db.SaveChangesAsync(ct);
        return changed;
    }
}
