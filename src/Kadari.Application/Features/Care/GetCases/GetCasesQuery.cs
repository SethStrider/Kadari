using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.GetCases;

/// <summary>An asset's cases, open ones first, newest first. Customer-scoped.</summary>
public sealed record GetCasesQuery(Guid CustomerId, Guid AssetId) : IQuery<IReadOnlyList<CaseSummaryDto>>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

internal sealed class GetCasesHandler(IKadariDbContext db, OwnedAssets assets, TimeProvider clock)
    : IQueryHandler<GetCasesQuery, IReadOnlyList<CaseSummaryDto>>
{
    public async Task<IReadOnlyList<CaseSummaryDto>> Handle(GetCasesQuery query, CancellationToken ct)
    {
        var asset = await assets.GetAsync(query.CustomerId, query.AssetId, ct);
        var cases = await db.Set<AssetCase>().AsNoTracking()
            .Include(c => c.Commitments)
            .Where(c => c.AssetId == asset.Id)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return cases
            .OrderBy(c => c.IsClosed)
            .ThenByDescending(c => c.OpenedAt)
            .Select(c => CaseView.Summary(c, today))
            .ToList();
    }
}
