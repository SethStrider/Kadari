using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Tenancy;

/// <summary>
/// The explicit tenant-scoping helper (D2/D11, deferred from build item 0.3). Owner-scoping already isolates
/// a consumer's own reads (a subject belongs to one tenant), so this is defence-in-depth today and the seam
/// the deferred insurer console reads through: any query over tenant-scoped rows narrows to the current
/// tenant, so one tenant's cohort can never surface another's rows.
/// </summary>
public static class TenantScopeExtensions
{
    public static IQueryable<T> ForCurrentTenant<T>(this IQueryable<T> source, ICurrentTenant tenant)
        where T : class, ITenantScoped
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tenant);

        var current = tenant.Current;
        return source.Where(x => x.TenantId == current);
    }

    /// <summary>
    /// The tenant a customer's own data lives in: their insurer's when they are on an insurer's book (D11), otherwise
    /// the platform's default. A customer's reads narrow to this, not to the caller's tenant: an insured customer, or
    /// a staff member reading on their behalf, must still see the assets their insurer holds for them.
    /// </summary>
    public static async Task<TenantId> TenantOfCustomerAsync(
        this IKadariDbContext db, CustomerId customerId, CancellationToken cancellationToken)
        => await db.TenantMemberships.AsNoTracking()
               .Where(m => m.CustomerId == customerId)
               .Select(m => (TenantId?)m.TenantId)
               .FirstOrDefaultAsync(cancellationToken)
           ?? TenantId.Default;

    public static IQueryable<T> ForTenant<T>(this IQueryable<T> source, TenantId tenant)
        where T : class, ITenantScoped
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Where(x => x.TenantId == tenant);
    }
}
