using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Exit;

/// <summary>A customer may only export their own asset — the query names the customer, so it must be theirs.</summary>
internal sealed class ExportAssetForSaleOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<ExportAssetForSaleQuery>
{
    public async Task<ResourceOwner?> ResolveAsync(ExportAssetForSaleQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
