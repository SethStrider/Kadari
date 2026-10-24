using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

/// <summary>
/// Keeps a case's reminders in line with its promises and appointments (CARE/14 C11 "a provider promise creates a due
/// item"): a provider promise is chased the day after it falls due if it is still open (<see
/// cref="CareReminderType.CommitmentOverdue"/>), an appointment is flagged the day before. Settled or cancelled ones
/// are cancelled. Idempotent through the reminder's unique dedupe key; the existing sweep delivers them.
/// </summary>
internal sealed class CaseReminders(IKadariDbContext db, TimeProvider clock)
{
    private static readonly CareReminderType[] Owned = [CareReminderType.CommitmentOverdue, CareReminderType.AppointmentDue];

    public async Task SyncAsync(AssetCase @case, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var wanted = new Dictionary<string, (CareReminderType Type, DateOnly DueOn, string SourceType, Guid SourceId)>();

        foreach (var c in @case.Commitments.Where(c => c.Status == CommitmentStatus.Open && c.Party == CommitmentParty.Provider))
        {
            Want(CareReminderType.CommitmentOverdue, c.DueOn.AddDays(1), "CaseCommitment", c.Id.Value);
        }

        foreach (var a in @case.Appointments.Where(a => a.Status == AppointmentStatus.Booked))
        {
            var dueOn = DateOnly.FromDateTime(a.StartsAt.UtcDateTime).AddDays(-1);
            Want(CareReminderType.AppointmentDue, dueOn < today ? today : dueOn, "CaseAppointment", a.Id.Value);
        }

        var sourceIds = @case.Commitments.Select(c => c.Id.Value).Concat(@case.Appointments.Select(a => a.Id.Value)).ToList();
        var existing = await db.Set<CareReminder>()
            .Where(r => r.AssetId == @case.AssetId && Owned.Contains(r.Type) && sourceIds.Contains(r.SourceId))
            .ToListAsync(ct);
        var known = existing.Select(r => r.DedupeKey).ToHashSet();

        foreach (var (key, w) in wanted.Where(w => !known.Contains(w.Key)))
        {
            db.Set<CareReminder>().Add(CareReminder.Schedule(@case.AssetId, w.Type, w.DueOn, w.SourceType, w.SourceId));
        }

        foreach (var stale in existing.Where(r => !wanted.ContainsKey(r.DedupeKey)))
        {
            stale.Cancel(now);
        }

        void Want(CareReminderType type, DateOnly dueOn, string sourceType, Guid sourceId) =>
            wanted[CareReminder.KeyFor(@case.AssetId, type, sourceId, dueOn)] = (type, dueOn, sourceType, sourceId);
    }
}
