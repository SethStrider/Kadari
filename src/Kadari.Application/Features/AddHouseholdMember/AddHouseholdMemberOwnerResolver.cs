using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.AddHouseholdMember;

/// <summary>Only the primary owner manages their asset's household — the command names that customer.</summary>
internal sealed class AddHouseholdMemberOwnerResolver(IKadariDbContext db)
    : IResourceOwnerResolver<AddHouseholdMemberCommand>
{
    public async Task<ResourceOwner?> ResolveAsync(AddHouseholdMemberCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.Set<Customer>().AsNoTracking()
            .AnyAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);

        return exists ? ResourceOwner.Customer(request.CustomerId) : null;
    }
}
