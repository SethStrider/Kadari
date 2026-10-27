using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Features.Care.GetCareSummary;

/// <summary>A customer may only read Care for their own asset — the query names the customer.</summary>
internal sealed class GetCareSummaryOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetCareSummaryQuery>
{
    public Task<ResourceOwner?> ResolveAsync(GetCareSummaryQuery request, CancellationToken cancellationToken)
        => CustomerExists.ResolveAsync(db, request.CustomerId, cancellationToken);
}
