using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Common;
using Kadari.Contracts.Care;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.DispatchDueCareReminders;

/// <summary>
/// The reminder sweep (CARE/14 C6), run by the Worker on a schedule: every reminder whose day has come is queued and
/// handed to delivery through the outbox — in this transaction, with a message id derived from the reminder, so a
/// sweep that runs twice cannot send twice. Reminders about an offer that is no longer open are cancelled instead.
/// Registration offers and registrations whose deadline has passed expire.
/// </summary>
/// <remarks>Unscoped: a system sweep across all customers, run by the Worker, never by a customer.</remarks>
public sealed record DispatchDueCareRemindersCommand(int MaxReminders = 500) : ICommand<DispatchDueCareRemindersResponse>, IUnscopedRequest;

public sealed record DispatchDueCareRemindersResponse(int Queued, int Cancelled, int OffersExpired, int RegistrationsExpired);

internal sealed class DispatchDueCareRemindersHandler(IKadariDbContext db, IIntegrationEventPublisher publisher, TimeProvider clock)
    : ICommandHandler<DispatchDueCareRemindersCommand, DispatchDueCareRemindersResponse>
{
    public async Task<DispatchDueCareRemindersResponse> Handle(DispatchDueCareRemindersCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // Deadlines first, so a reminder about an offer that just lapsed is cancelled rather than sent.
        var lapsedOffers = await db.Set<RegistrationOpportunity>()
            .Where(o => o.Status == OpportunityStatus.Available && o.DeadlineOn < today)
            .ToListAsync(ct);
        var offersExpired = lapsedOffers.Count(o => o.ExpireIfLapsed(today, now));

        var lapsedRegistrations = await (
                from r in db.Set<CoverageRegistration>()
                join o in db.Set<RegistrationOpportunity>() on r.OpportunityId equals o.Id
                where (r.Status == RegistrationStatus.WaitingUserStep || r.Status == RegistrationStatus.FailedRetryable)
                      && o.DeadlineOn < today
                select r)
            .ToListAsync(ct);
        foreach (var registration in lapsedRegistrations)
        {
            registration.Expire(now);
        }

        var due = await db.Set<CareReminder>()
            .Where(r => (r.Status == CareReminderStatus.Scheduled && r.DueOn <= today)
                        || (r.Status == CareReminderStatus.Snoozed && r.SnoozedUntil <= today))
            .OrderBy(r => r.DueOn)
            .Take(cmd.MaxReminders)
            .ToListAsync(ct);

        var offerIds = due.Where(r => r.Type == CareReminderType.RegistrationDeadline).Select(r => new RegistrationOpportunityId(r.SourceId)).ToList();
        var openOffers = (await db.Set<RegistrationOpportunity>().AsNoTracking()
                .Where(o => offerIds.Contains(o.Id) && o.Status == OpportunityStatus.Available)
                .Select(o => o.Id)
                .ToListAsync(ct))
            .ToHashSet();
        var expiredNow = lapsedOffers.Where(o => o.Status == OpportunityStatus.Expired).Select(o => o.Id).ToHashSet();

        int queued = 0, cancelled = 0;
        foreach (var reminder in due)
        {
            var offer = new RegistrationOpportunityId(reminder.SourceId);
            if (reminder.Type == CareReminderType.RegistrationDeadline && (!openOffers.Contains(offer) || expiredNow.Contains(offer)))
            {
                reminder.Cancel(now);
                cancelled++;
                continue;
            }

            var fireDate = reminder.Status == CareReminderStatus.Snoozed ? reminder.SnoozedUntil : reminder.DueOn;
            if (reminder.Queue(today, now))
            {
                await publisher.PublishAsync(
                    new CareReminderDue(reminder.Id.Value),
                    DeterministicGuid.From($"care-reminder:{reminder.Id.Value:N}:{fireDate:yyyyMMdd}"), ct);
                queued++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new DispatchDueCareRemindersResponse(queued, cancelled, offersExpired, lapsedRegistrations.Count);
    }
}
