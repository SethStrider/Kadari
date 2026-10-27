using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.GetCoverage;

/// <summary>A customer may only read coverage for their own asset — the query names the customer.</summary>
internal sealed class GetCoverageOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetCoverageQuery>
{
    public Task<ResourceOwner?> ResolveAsync(GetCoverageQuery request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
