using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.GetLedger;

/// <summary>A customer may only read their own ledger — the query names the customer, so it must be theirs.</summary>
internal sealed class GetLedgerOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<GetLedgerQuery>
{
    public async Task<ResourceOwner?> ResolveAsync(GetLedgerQuery request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
