using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.MintAssetPassport;

/// <summary>A customer may only mint a passport for their own asset — the command names the customer.</summary>
internal sealed class MintAssetPassportOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<MintAssetPassportCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(MintAssetPassportCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
