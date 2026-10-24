using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Care.Services;
using Kadari.Application.Features.CareOps.Services;
using Kadari.Domain.Audit;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.ResolveOpsWorkItem;

/// <summary>An operator resolves a work item, saying what kind of knowledge it produced (CARE/14 C12): reusable registry
/// knowledge, case-specific, needs a policy review, or a product bug. Audited.</summary>
/// <remarks>Unscoped: a staff action, gated by <c>CareOpsReview</c> at the edge.</remarks>
public sealed record ResolveOpsWorkItemCommand(Guid WorkItemId, string Resolution, string Notes) : ICommand<OpsWorkItemDto>, IUnscopedRequest;

internal sealed class ResolveOpsWorkItemHandler(IKadariDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ResolveOpsWorkItemCommand, OpsWorkItemDto>
{
    public async Task<OpsWorkItemDto> Handle(ResolveOpsWorkItemCommand cmd, CancellationToken ct)
    {
        var item = await db.Set<OpsWorkItem>().FirstOrDefaultAsync(w => w.Id == new OpsWorkItemId(cmd.WorkItemId), ct)
            ?? throw new NotFoundException(nameof(OpsWorkItem), cmd.WorkItemId);
        var operatorId = user.AccountId ?? throw new ForbiddenException("Only a signed-in operator can resolve work.");
        var now = clock.GetUtcNow();

        item.Resolve(operatorId, CareEnum.Parse<OpsResolution>(cmd.Resolution), cmd.Notes, now);
        db.Set<AuditEvent>().Add(AuditEvent.Record(
            ActorKind.Account, "OpsWorkItem", item.Id.Value, "CareOpsWorkItemResolved", now, actorId: operatorId,
            previousState: item.WorkType.ToString(), newState: item.Resolution.ToString()));

        await db.SaveChangesAsync(ct);
        return OpsWorkItemDto.From(item, now);
    }
}
