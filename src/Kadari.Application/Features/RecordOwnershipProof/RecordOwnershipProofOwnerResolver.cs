using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.RecordOwnershipProof;

/// <summary>A customer may only prove ownership of their own asset — the command names the customer.</summary>
internal sealed class RecordOwnershipProofOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<RecordOwnershipProofCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(RecordOwnershipProofCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
