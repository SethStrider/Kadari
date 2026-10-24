using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Care.Services;
using Kadari.Application.Features.CareOps.Services;
using Kadari.Domain.Audit;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.CareOps.SetCaseRoute;

/// <summary>
/// An operator found where an unknown-route case goes (CARE/11 §12). Records a human-override routing decision (the
/// engine's decision stays as it was), gives the case the counterparty as the operator found it, and resolves the work
/// item. The owner still confirms the route before anything is sent — Ops acts only within the authority the customer
/// gave (CARE/11 §8). Reusable knowledge is marked for the registry; it becomes a rule only through the registry import.
/// </summary>
/// <remarks>Unscoped: a staff action, gated by <c>CareOpsReview</c> at the edge.</remarks>
public sealed record SetCaseRouteCommand(
    Guid WorkItemId,
    string RouteType,
    string TargetName,
    string ChannelType,
    string? ChannelValue,
    string Instructions,
    string Resolution,
    string Notes) : ICommand<OpsWorkItemDto>, IUnscopedRequest;

internal sealed class SetCaseRouteHandler(IKadariDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<SetCaseRouteCommand, OpsWorkItemDto>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<OpsWorkItemDto> Handle(SetCaseRouteCommand cmd, CancellationToken ct)
    {
        var item = await db.Set<OpsWorkItem>().FirstOrDefaultAsync(w => w.Id == new OpsWorkItemId(cmd.WorkItemId), ct)
            ?? throw new NotFoundException(nameof(OpsWorkItem), cmd.WorkItemId);
        if (item.WorkType != OpsWorkType.RouteUnknown || item.CaseId is not { } caseId)
        {
            throw new ConflictException("Only an unknown-route work item sets a case's route.");
        }

        var operatorId = user.AccountId ?? throw new ForbiddenException("Only a signed-in operator can set a route.");
        var @case = await db.Set<AssetCase>().FirstOrDefaultAsync(c => c.Id == caseId, ct)
            ?? throw new NotFoundException(nameof(AssetCase), caseId.Value);
        if (@case.Status != CaseStatus.Preparing)
        {
            throw new ConflictException("The case has moved on; its route can no longer change.");
        }

        var now = clock.GetUtcNow();
        var route = CareEnum.Parse<RouteType>(cmd.RouteType);
        var snapshot = JsonSerializer.Serialize(new
        {
            source = "care-ops",
            workItemId = item.Id.Value,
            operatorId,
            routeType = route.ToString(),
            cmd.TargetName,
            cmd.ChannelType,
        }, Json);
        var decision = RoutingDecision.Override(@case.AssetId, route, cmd.Notes, snapshot, now);
        db.Set<RoutingDecision>().Add(decision);

        @case.ApplyManualRoute(decision, cmd.TargetName, CareEnum.Parse<ContactChannelType>(cmd.ChannelType), cmd.ChannelValue,
            cmd.Instructions, now);
        item.Resolve(operatorId, CareEnum.Parse<OpsResolution>(cmd.Resolution), cmd.Notes, now);

        db.Set<AuditEvent>().Add(AuditEvent.Record(
            ActorKind.Account, "AssetCase", @case.Id.Value, "CareRouteSetByOps", now, actorId: operatorId,
            previousState: RouteType.ResearchRequired.ToString(), newState: route.ToString()));

        await db.SaveChangesAsync(ct);
        return OpsWorkItemDto.From(item, now);
    }
}
