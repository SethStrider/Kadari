using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.TransferAsset;

/// <summary>Only the current owner may transfer their own asset — the command names that customer.</summary>
internal sealed class TransferAssetOwnerResolver(IKadariDbContext db) : IResourceOwnerResolver<TransferAssetCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(TransferAssetCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
