using MediatR;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Care.Events;

namespace Kadari.Application.Features.Care.Reactions;

/// <summary>Activated extra cover is part of the asset's story (CARE/05 C28).</summary>
internal sealed class OnWarrantyRegistrationCompletedAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<WarrantyRegistrationCompletedDomainEvent>>
{
    public Task Handle(DomainEventNotification<WarrantyRegistrationCompletedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        db.AssetHistory.Add(AssetHistoryEntry.Record(
            e.AssetId, AssetHistoryKind.RegistrationCompleted, $"Product registered — reference {e.ExternalReference}.",
            e.At, "CoverageRegistration", e.RegistrationId.Value));
        return Task.CompletedTask;
    }
}

/// <summary>A confirmed registration can satisfy a rule's registration requirement and add its extension, so the cover
/// is worked out again in the same transaction (CARE/03 Module C step 8 "update coverage dates").</summary>
internal sealed class OnWarrantyRegistrationCompletedReevaluateCare(IKadariDbContext db, CareCoverage coverage)
    : INotificationHandler<DomainEventNotification<WarrantyRegistrationCompletedDomainEvent>>
{
    public async Task Handle(DomainEventNotification<WarrantyRegistrationCompletedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var profile = await db.CareProfiles.Include(p => p.Coverage).FirstOrDefaultAsync(p => p.AssetId == e.AssetId, ct);
        if (profile is null)
        {
            return;
        }

        // The registration is saved by now (the handler saved before dispatch), so the engine sees it.
        var asset = await db.Assets.Include(a => a.Evidence).FirstAsync(a => a.Id == e.AssetId, ct);
        await coverage.EvaluateAsync(asset, profile, ct);
    }
}
