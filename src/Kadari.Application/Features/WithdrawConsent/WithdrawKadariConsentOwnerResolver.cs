using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.WithdrawConsent;

/// <summary>A customer may only withdraw their own consent — the command names the customer.</summary>
internal sealed class WithdrawKadariConsentOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<WithdrawKadariConsentCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(WithdrawKadariConsentCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
