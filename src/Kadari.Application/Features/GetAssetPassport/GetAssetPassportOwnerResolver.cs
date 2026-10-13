using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.GetAssetPassport;

/// <summary>A customer may only read a passport for their own asset — the query names the customer.</summary>
internal sealed class GetAssetPassportOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<GetAssetPassportQuery>
{
    public async Task<ResourceOwner?> ResolveAsync(GetAssetPassportQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
