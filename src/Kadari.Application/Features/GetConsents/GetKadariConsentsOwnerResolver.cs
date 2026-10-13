using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.GetConsents;

/// <summary>A customer may only read their own consents — the query names the customer.</summary>
internal sealed class GetKadariConsentsOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<GetKadariConsentsQuery>
{
    public async Task<ResourceOwner?> ResolveAsync(GetKadariConsentsQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
