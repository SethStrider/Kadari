using Kadari.Application.Abstractions.Access;
using Kadari.Domain.Assets;

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
}
