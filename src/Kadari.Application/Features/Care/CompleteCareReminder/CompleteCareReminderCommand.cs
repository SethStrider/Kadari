using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;

namespace Kadari.Application.Features.Care.CompleteCareReminder;

/// <summary>Mark a reminder done — the owner acted on it (CARE/13 §6). Customer-scoped.</summary>
public sealed record CompleteCareReminderCommand(Guid CustomerId, Guid ReminderId) : ICommand<CareReminderStatusResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class CompleteCareReminderHandler(IKadariDbContext db, OwnedReminders reminders, TimeProvider clock)
    : ICommandHandler<CompleteCareReminderCommand, CareReminderStatusResponse>
{
    public async Task<CareReminderStatusResponse> Handle(CompleteCareReminderCommand cmd, CancellationToken ct)
    {
        var reminder = await reminders.GetForUpdateAsync(cmd.CustomerId, cmd.ReminderId, ct);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        reminder.Complete(now);
        await db.SaveChangesAsync(ct);
        return new CareReminderStatusResponse(reminder.Id.Value, reminder.Status.ToString());
    }
}
