using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Tenancy;

/// <summary>
/// Resolves which tenant a customer's newly captured assets belong to (D2/D11). A customer with a
/// <see cref="TenantMembership"/> (assigned to an insurer cohort) captures into that tenant; everyone else
/// captures into <see cref="TenantId.Default"/>, the consumer-direct platform tenant. Shared by the capture
/// pipeline so a single place decides tenancy at registration time.
/// </summary>
internal sealed class TenantMembershipResolver(IKadariDbContext db)
{
    public async Task<TenantId> ResolveForCustomerAsync(CustomerId customerId, CancellationToken ct)
    {
        var membership = await db.TenantMemberships.AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .Select(m => (TenantId?)m.TenantId)
            .FirstOrDefaultAsync(ct);

        return membership ?? TenantId.Default;
    }
}
