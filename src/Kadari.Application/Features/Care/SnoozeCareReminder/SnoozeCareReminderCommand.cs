using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;

namespace Kadari.Application.Features.Care.SnoozeCareReminder;

/// <summary>Remind me later (CARE/05 C07): the reminder comes back after <see cref="Days"/> days. Customer-scoped.</summary>
public sealed record SnoozeCareReminderCommand(Guid CustomerId, Guid ReminderId, int Days) : ICommand<CareReminderStatusResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class SnoozeCareReminderHandler(IKadariDbContext db, OwnedReminders reminders, TimeProvider clock)
    : ICommandHandler<SnoozeCareReminderCommand, CareReminderStatusResponse>
{
    public async Task<CareReminderStatusResponse> Handle(SnoozeCareReminderCommand cmd, CancellationToken ct)
    {
        var reminder = await reminders.GetForUpdateAsync(cmd.CustomerId, cmd.ReminderId, ct);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        reminder.Snooze(today.AddDays(cmd.Days), today);
        await db.SaveChangesAsync(ct);
        return new CareReminderStatusResponse(reminder.Id.Value, reminder.Status.ToString());
    }
}
