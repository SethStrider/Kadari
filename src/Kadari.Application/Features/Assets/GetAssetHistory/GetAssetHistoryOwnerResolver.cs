using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Assets.GetAssetHistory;

/// <summary>A customer may only read their own asset's history — the query names the customer.</summary>
internal sealed class GetAssetHistoryOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetAssetHistoryQuery>
{
    public Task<ResourceOwner?> ResolveAsync(GetAssetHistoryQuery request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
