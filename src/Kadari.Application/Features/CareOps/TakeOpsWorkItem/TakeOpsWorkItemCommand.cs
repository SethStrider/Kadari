using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.CareOps.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.TakeOpsWorkItem;

/// <summary>An operator takes a work item, so two people don't research the same thing.</summary>
/// <remarks>Unscoped: a staff action, gated by <c>CareOpsReview</c> at the edge.</remarks>
public sealed record TakeOpsWorkItemCommand(Guid WorkItemId) : ICommand<OpsWorkItemDto>, IUnscopedRequest;

internal sealed class TakeOpsWorkItemHandler(IKadariDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<TakeOpsWorkItemCommand, OpsWorkItemDto>
{
    public async Task<OpsWorkItemDto> Handle(TakeOpsWorkItemCommand cmd, CancellationToken ct)
    {
        var item = await db.Set<OpsWorkItem>().FirstOrDefaultAsync(w => w.Id == new OpsWorkItemId(cmd.WorkItemId), ct)
            ?? throw new NotFoundException(nameof(OpsWorkItem), cmd.WorkItemId);
        item.Take(user.AccountId ?? throw new ForbiddenException("Only a signed-in operator can take work."));
        await db.SaveChangesAsync(ct);
        return OpsWorkItemDto.From(item, clock.GetUtcNow());
    }
}
