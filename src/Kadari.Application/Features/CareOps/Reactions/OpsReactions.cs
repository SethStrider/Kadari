using MediatR;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.CareOps.Services;
using Kadari.Domain.Care;
using Kadari.Domain.Care.Events;

namespace Kadari.Application.Features.CareOps.Reactions;

/// <summary>No verified route: a person finds one (CARE/11 §12 unknown-route handling).</summary>
internal sealed class OnCaseNeedsRouteRaiseOpsWork(IKadariDbContext db, OpsQueue queue)
    : INotificationHandler<DomainEventNotification<CaseNeedsRouteDomainEvent>>
{
    public async Task Handle(DomainEventNotification<CaseNeedsRouteDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var category = await db.CareProfiles.AsNoTracking().Where(p => p.AssetId == e.AssetId).Select(p => p.Category).FirstOrDefaultAsync(ct);
        await queue.RaiseAsync(OpsWorkType.RouteUnknown, OpsSeverity.P2, e.AssetId, e.CaseId, e.CaseId.Value, "no-verified-route",
            $"No verified route for this {category} problem — find where it goes and confirm it with the owner.", ct);
    }
}

/// <summary>The safety gate tripped: a person reviews it at once (CARE/11 §5 P0).</summary>
internal sealed class OnAssetCaseOpenedRaiseSafetyReview(OpsQueue queue)
    : INotificationHandler<DomainEventNotification<AssetCaseOpenedDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetCaseOpenedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        return e.SafetyFlagged
            ? queue.RaiseAsync(OpsWorkType.SafetyReview, OpsSeverity.P0, e.AssetId, e.CaseId, e.CaseId.Value, "safety-gate",
                "The safety gate tripped on a reported problem — review it now.", ct)
            : Task.CompletedTask;
    }
}

/// <summary>A registration that needs a person (CARE/11 §11).</summary>
internal sealed class OnWarrantyRegistrationFailedRaiseOpsWork(OpsQueue queue)
    : INotificationHandler<DomainEventNotification<WarrantyRegistrationFailedDomainEvent>>
{
    public Task Handle(DomainEventNotification<WarrantyRegistrationFailedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        return e.Status == RegistrationStatus.FailedManual
            ? queue.RaiseAsync(OpsWorkType.RegistrationFailed, OpsSeverity.P2, e.AssetId, null, e.RegistrationId.Value,
                "registration-failed", "A warranty registration failed and needs a person before its deadline.", ct)
            : Task.CompletedTask;
    }
}

/// <summary>Registry rules disagree about an asset's cover: KADARI never picks, a person checks (CARE/11 §4 RULE_CONFLICT).</summary>
internal sealed class OnCoverageEvaluatedRaiseRuleConflict(OpsQueue queue)
    : INotificationHandler<DomainEventNotification<CoverageEvaluatedDomainEvent>>
{
    public Task Handle(DomainEventNotification<CoverageEvaluatedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        return e.CurrentState == CoverageState.NeedsReview && e.IsMaterialChange
            ? queue.RaiseAsync(OpsWorkType.RuleConflict, OpsSeverity.P2, e.AssetId, null, e.DecisionId.Value, "rule-conflict",
                "Registry rules disagree about this product's cover — check the sources and correct the registry.", ct)
            : Task.CompletedTask;
    }
}

/// <summary>A closed case needs no one: its open work closes with it.</summary>
internal sealed class OnAssetCaseClosedCloseOpsWork(OpsQueue queue)
    : INotificationHandler<DomainEventNotification<AssetCaseClosedDomainEvent>>
{
    public Task Handle(DomainEventNotification<AssetCaseClosedDomainEvent> notification, CancellationToken ct) =>
        queue.CloseForCaseAsync(notification.DomainEvent.CaseId, "The case closed.", ct);
}
