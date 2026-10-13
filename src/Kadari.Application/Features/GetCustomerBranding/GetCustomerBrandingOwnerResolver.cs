using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.GetCustomerBranding;

/// <summary>A customer may only read the branding of their own app — the query names the customer.</summary>
internal sealed class GetCustomerBrandingOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<GetCustomerBrandingQuery>
{
    public async Task<ResourceOwner?> ResolveAsync(GetCustomerBrandingQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
