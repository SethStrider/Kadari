using MediatR;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Assets;
using Kadari.Domain.Assets.Events;
using Kadari.Domain.Care;
using Kadari.Domain.Care.Events;

namespace Kadari.Application.Features.Care.Reactions;

/// <summary>Switching Care on is part of the asset's story.</summary>
internal sealed class OnCareEnabledAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<CareEnabledDomainEvent>>
{
    public Task Handle(DomainEventNotification<CareEnabledDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        db.AssetHistory.Add(AssetHistoryEntry.Record(
            e.AssetId, AssetHistoryKind.CareEnabled, "KADARI Care switched on.", e.At, "CareProfile", e.CareProfileId.Value));
        return Task.CompletedTask;
    }
}

/// <summary>
/// A material change to verified cover is written to the asset's history (CARE/12 §11); a re-check that found the
/// same thing, or a move between "likely" and "unknown", is not — history holds what the owner would want to know
/// years later, not every evaluation. The entry points at the decision that produced it.
/// </summary>
internal sealed class OnCoverageEvaluatedAppendHistory(IKadariDbContext db)
    : INotificationHandler<DomainEventNotification<CoverageEvaluatedDomainEvent>>
{
    private static readonly CoverageState[] Verified = [CoverageState.Active, CoverageState.Expiring, CoverageState.Expired];

    public Task Handle(DomainEventNotification<CoverageEvaluatedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        if (!e.IsMaterialChange || (!Verified.Contains(e.CurrentState) && !Verified.Contains(e.PreviousState)))
        {
            return Task.CompletedTask;
        }

        var date = e.CurrentExpiresOn?.ToString("d MMMM yyyy");
        var summary = e.CurrentState switch
        {
            CoverageState.Active => $"Warranty confirmed — covered until {date}.",
            CoverageState.Expiring => $"Warranty confirmed — it ends on {date}.",
            CoverageState.Expired => $"Warranty ended on {date}.",
            _ => "KADARI is re-checking this product's cover.",
        };
        var kind = Verified.Contains(e.PreviousState) ? AssetHistoryKind.CoverageChanged : AssetHistoryKind.CoverageFound;

        db.AssetHistory.Add(AssetHistoryEntry.Record(e.AssetId, kind, summary, e.At, "CoverageDecision", e.DecisionId.Value));
        return Task.CompletedTask;
    }
}

/// <summary>
/// New purchase facts or a new receipt can change the cover, so a Care-enabled asset is re-evaluated in the same
/// transaction as the evidence — the card is never behind its evidence. Assets without Care are left alone.
/// </summary>
internal sealed class OnAssetEvidenceAttachedReevaluateCare(IKadariDbContext db, CareCoverage coverage)
    : INotificationHandler<DomainEventNotification<AssetEvidenceAttachedDomainEvent>>
{
    public async Task Handle(DomainEventNotification<AssetEvidenceAttachedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        if (e.Kind is not (AssetEvidenceKind.Purchase or AssetEvidenceKind.Ownership))
        {
            return;
        }

        var profile = await db.CareProfiles.Include(p => p.Coverage).FirstOrDefaultAsync(p => p.AssetId == e.AssetId, ct);
        if (profile is null)
        {
            return;
        }

        var asset = await db.Assets.Include(a => a.Evidence).FirstAsync(a => a.Id == e.AssetId, ct);
        await coverage.EvaluateAsync(asset, profile, ct);
    }
}
