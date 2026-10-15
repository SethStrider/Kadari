using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Development.SeedDemoHousehold;

/// <summary>A customer may only seed their own ledger — the command names the customer.</summary>
internal sealed class SeedDemoHouseholdOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<SeedDemoHouseholdCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(SeedDemoHouseholdCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
