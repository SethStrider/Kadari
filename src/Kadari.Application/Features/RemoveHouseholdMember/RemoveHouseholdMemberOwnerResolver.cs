using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.RemoveHouseholdMember;

/// <summary>Only the primary owner manages their asset's household — the command names that customer.</summary>
internal sealed class RemoveHouseholdMemberOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<RemoveHouseholdMemberCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(RemoveHouseholdMemberCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
