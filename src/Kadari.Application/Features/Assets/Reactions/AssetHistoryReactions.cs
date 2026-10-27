using MediatR;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Assets.Events;

namespace Kadari.Application.Features.Assets.Reactions;

// The asset's own lifecycle events, written into its permanent history in the same transaction as the change
// (CARE/12 §11: "one asset identity, one history"). One reaction per event, named On{Event}{Action}.

/// <summary>A captured item joins the ledger.</summary>
internal sealed class OnAssetRegisteredAppendHistory(IKadariDbContext db, TimeProvider clock)
    : INotificationHandler<DomainEventNotification<AssetRegisteredDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetRegisteredDomainEvent> notification, CancellationToken ct)
    {
        db.AssetHistory.Add(AssetHistoryEntry.Record(
            notification.DomainEvent.AssetId, AssetHistoryKind.Registered, "Added to KADARI.", clock.GetUtcNow()));
        return Task.CompletedTask;
    }
}

/// <summary>The item was given or sold to someone else.</summary>
internal sealed class OnAssetTransferredAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<AssetTransferredDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetTransferredDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        db.AssetHistory.Add(AssetHistoryEntry.Record(e.AssetId, AssetHistoryKind.Transferred, "Ownership transferred.", e.At));
        return Task.CompletedTask;
    }
}

/// <summary>The item passed to an heir.</summary>
internal sealed class OnAssetInheritedAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<AssetInheritedDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetInheritedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        db.AssetHistory.Add(AssetHistoryEntry.Record(e.AssetId, AssetHistoryKind.Inherited, "Passed on through an estate.", e.At));
        return Task.CompletedTask;
    }
}

/// <summary>The owner reported the item stolen.</summary>
internal sealed class OnAssetReportedStolenAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<AssetReportedStolenDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetReportedStolenDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        db.AssetHistory.Add(AssetHistoryEntry.Record(e.AssetId, AssetHistoryKind.ReportedStolen, "Reported stolen.", e.ReportedAt));
        return Task.CompletedTask;
    }
}

/// <summary>A stolen item was recovered.</summary>
internal sealed class OnAssetRecoveredAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<AssetRecoveredDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetRecoveredDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        db.AssetHistory.Add(AssetHistoryEntry.Record(e.AssetId, AssetHistoryKind.Recovered, "Recovered.", e.RecoveredAt));
        return Task.CompletedTask;
    }
}
