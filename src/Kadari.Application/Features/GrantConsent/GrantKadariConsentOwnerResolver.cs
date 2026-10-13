using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.GrantConsent;

/// <summary>A customer may only grant their own consent — the command names the customer.</summary>
internal sealed class GrantKadariConsentOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<GrantKadariConsentCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(GrantKadariConsentCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
