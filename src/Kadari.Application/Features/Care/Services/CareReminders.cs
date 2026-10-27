using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

/// <summary>
/// Keeps an asset's scheduled reminders in line with its cover and offers (<see cref="ReminderPlan"/>): adds what is
/// missing (by the unique dedupe key, so nothing is scheduled twice), cancels scheduled ones that are no longer true —
/// the dates moved, the offer was taken — and leaves anything already delivered alone. Called after every evaluation.
/// </summary>
internal sealed class CareReminders(IKadariDbContext db, TimeProvider clock)
{
    private static readonly CareReminderType[] Planned =
        [CareReminderType.Warranty90, CareReminderType.Warranty30, CareReminderType.RegistrationDeadline];

    public async Task SyncAsync(CareProfile profile, IReadOnlyList<RegistrationOpportunity> openOffers, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var plan = ReminderPlan.For(profile, openOffers, DateOnly.FromDateTime(now.UtcDateTime));
        var wanted = plan.ToDictionary(p => CareReminder.KeyFor(profile.AssetId, p.Type, p.SourceId, p.DueOn));

        var existing = await db.Set<CareReminder>()
            .Where(r => r.AssetId == profile.AssetId && Planned.Contains(r.Type))
            .ToListAsync(ct);
        var known = existing.Select(r => r.DedupeKey).ToHashSet();

        foreach (var (key, p) in wanted.Where(w => !known.Contains(w.Key)))
        {
            db.Set<CareReminder>().Add(CareReminder.Schedule(profile.AssetId, p.Type, p.DueOn, p.SourceType, p.SourceId));
        }

        foreach (var stale in existing.Where(r => !wanted.ContainsKey(r.DedupeKey)))
        {
            stale.Cancel(now);
        }
    }
}
