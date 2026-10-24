using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;

namespace Kadari.Application.Features.Care.DismissCareReminder;

/// <summary>Dismiss a reminder (CARE/05 C07); it is kept, closed. Customer-scoped.</summary>
public sealed record DismissCareReminderCommand(Guid CustomerId, Guid ReminderId) : ICommand<CareReminderStatusResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class DismissCareReminderHandler(IKadariDbContext db, OwnedReminders reminders, TimeProvider clock)
    : ICommandHandler<DismissCareReminderCommand, CareReminderStatusResponse>
{
    public async Task<CareReminderStatusResponse> Handle(DismissCareReminderCommand cmd, CancellationToken ct)
    {
        var reminder = await reminders.GetForUpdateAsync(cmd.CustomerId, cmd.ReminderId, ct);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        reminder.Dismiss(now);
        await db.SaveChangesAsync(ct);
        return new CareReminderStatusResponse(reminder.Id.Value, reminder.Status.ToString());
    }
}
