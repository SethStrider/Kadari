using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.GetAsset;

/// <summary>A customer may only read their own assets — the query names the customer, so it must be theirs.</summary>
internal sealed class GetAssetOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetAssetQuery>
{
    public async Task<ResourceOwner?> ResolveAsync(GetAssetQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
