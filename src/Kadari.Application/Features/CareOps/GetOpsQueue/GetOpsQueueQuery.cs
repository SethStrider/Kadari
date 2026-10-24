using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Care.Services;
using Kadari.Application.Features.CareOps.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.GetOpsQueue;

/// <summary>The Care Ops queue (CARE/11 §4): unresolved work by severity then age, or resolved work when asked.</summary>
/// <remarks>Unscoped: a staff surface across all customers, gated by <c>CareOpsReview</c> at the edge.</remarks>
public sealed record GetOpsQueueQuery(bool Resolved = false, string? WorkType = null, int Take = 200)
    : IQuery<IReadOnlyList<OpsWorkItemDto>>, IUnscopedRequest;

internal sealed class GetOpsQueueHandler(IKadariDbContext db, TimeProvider clock) : IQueryHandler<GetOpsQueueQuery, IReadOnlyList<OpsWorkItemDto>>
{
    public async Task<IReadOnlyList<OpsWorkItemDto>> Handle(GetOpsQueueQuery query, CancellationToken ct)
    {
        var items = db.Set<OpsWorkItem>().AsNoTracking()
            .Where(w => query.Resolved ? w.Status == OpsWorkStatus.Resolved : w.Status != OpsWorkStatus.Resolved);
        if (CareEnum.TryParse<OpsWorkType>(query.WorkType, out var type))
        {
            items = items.Where(w => w.WorkType == type);
        }

        var list = await (query.Resolved
                ? items.OrderByDescending(w => w.ResolvedAt)
                : items.OrderBy(w => w.Severity).ThenBy(w => w.RaisedAt))
            .Take(Math.Clamp(query.Take, 1, 500))
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        // Severity is stored by name; P0…P3 sort correctly as text too, but sort here as well to be exact.
        return list
            .OrderBy(w => query.Resolved ? 0 : (int)w.Severity)
            .Select(w => OpsWorkItemDto.From(w, now))
            .ToList();
    }
}
