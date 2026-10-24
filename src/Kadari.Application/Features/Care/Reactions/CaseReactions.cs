using MediatR;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.Care.Events;

namespace Kadari.Application.Features.Care.Reactions;

/// <summary>The Care card's open-case count, recounted from the cases themselves when one opens.</summary>
internal sealed class OnAssetCaseOpenedCountOpenCases(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<AssetCaseOpenedDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetCaseOpenedDomainEvent> notification, CancellationToken ct) =>
        OpenCases.RecountAsync(db, notification.DomainEvent.AssetId, ct);
}

/// <summary>…and when one closes.</summary>
internal sealed class OnAssetCaseClosedCountOpenCases(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<AssetCaseClosedDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetCaseClosedDomainEvent> notification, CancellationToken ct) =>
        OpenCases.RecountAsync(db, notification.DomainEvent.AssetId, ct);
}

/// <summary>A resolved or rejected case is part of the asset's story (CARE/14 C9). The entry names the outcome only —
/// never what the owner or the provider wrote. A withdrawn case leaves no history.</summary>
internal sealed class OnAssetCaseClosedAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<AssetCaseClosedDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetCaseClosedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        if (e.Outcome is { } outcome)
        {
            var summary = outcome == CaseOutcome.Rejected
                ? "A claim about a problem was rejected."
                : $"A problem was resolved — {AssetCase.Describe(outcome)}.";
            db.AssetHistory.Add(AssetHistoryEntry.Record(e.AssetId, AssetHistoryKind.CaseResolved, summary, e.At, "AssetCase", e.CaseId.Value));
        }

        return Task.CompletedTask;
    }
}

/// <summary>A promise's reminder follows the promise.</summary>
internal sealed class OnCaseCommitmentChangedSyncReminders(IKadariDbContext db, CaseReminders reminders)
    : INotificationHandler<DomainEventNotification<CaseCommitmentChangedDomainEvent>>
{
    public Task Handle(DomainEventNotification<CaseCommitmentChangedDomainEvent> notification, CancellationToken ct) =>
        OpenCases.SyncRemindersAsync(db, reminders, notification.DomainEvent.CaseId, ct);
}

/// <summary>An appointment's reminder follows the appointment.</summary>
internal sealed class OnCaseAppointmentChangedSyncReminders(IKadariDbContext db, CaseReminders reminders)
    : INotificationHandler<DomainEventNotification<CaseAppointmentChangedDomainEvent>>
{
    public Task Handle(DomainEventNotification<CaseAppointmentChangedDomainEvent> notification, CancellationToken ct) =>
        OpenCases.SyncRemindersAsync(db, reminders, notification.DomainEvent.CaseId, ct);
}

/// <summary>A closed case leaves nothing to chase: its pending reminders are cancelled.</summary>
internal sealed class OnAssetCaseClosedSyncReminders(IKadariDbContext db, CaseReminders reminders)
    : INotificationHandler<DomainEventNotification<AssetCaseClosedDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetCaseClosedDomainEvent> notification, CancellationToken ct) =>
        OpenCases.SyncRemindersAsync(db, reminders, notification.DomainEvent.CaseId, ct);
}

file static class OpenCases
{
    private static readonly CaseStatus[] Closed = [CaseStatus.Resolved, CaseStatus.Rejected, CaseStatus.ClosedWithdrawn];

    public static async Task RecountAsync(IKadariDbContext db, AssetId assetId, CancellationToken ct)
    {
        var profile = await db.CareProfiles.FirstOrDefaultAsync(p => p.AssetId == assetId, ct);
        if (profile is null)
        {
            return;
        }

        // The case is saved by now (the handler saved before dispatch), so the count includes it.
        var open = await db.Set<AssetCase>().CountAsync(c => c.AssetId == assetId && !Closed.Contains(c.Status), ct);
        profile.CountOpenCases(open);
    }

    public static async Task SyncRemindersAsync(IKadariDbContext db, CaseReminders reminders, AssetCaseId caseId, CancellationToken ct)
    {
        var @case = await db.Set<AssetCase>()
            .Include(c => c.Commitments)
            .Include(c => c.Appointments)
            .FirstAsync(c => c.Id == caseId, ct);
        await reminders.SyncAsync(@case, ct);
    }
}
