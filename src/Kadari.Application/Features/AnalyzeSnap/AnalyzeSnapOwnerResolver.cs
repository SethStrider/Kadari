using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.AnalyzeSnap;

/// <summary>A customer may only capture into their own ledger — the command names the customer, so it must be theirs.</summary>
internal sealed class AnalyzeSnapOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<AnalyzeSnapCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(AnalyzeSnapCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
