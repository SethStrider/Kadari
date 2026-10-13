using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Access;
using Kadari.Domain.Assets;
using Kadari.Domain.Identity;

namespace Kadari.Application.Features.Tenancy;

/// <summary>
/// Confines an insurer's cohort reads to their own tenant (Kadari §27–28). The console read endpoints are
/// permission-gated but tenant-keyed; this is the second half — an <see cref="InsurerUser"/>-bound caller may
/// only read the tenant they are bound to. Reading any tenant is oversight, allowed only to a staff role that
/// holds the portfolio permission in its own right (a system administrator) — never to an Insurer whose
/// binding is missing, which fails closed (AS-04). Called by the tenant read handlers.
/// </summary>
internal sealed class InsurerTenantGuard(IKadariDbContext db, ICurrentUser currentUser)
{
    public async Task EnsureCanViewAsync(Guid requestedTenantId, CancellationToken ct)
    {
        if (currentUser.AccountId is not { } accountId)
        {
            return;   // no authenticated account (system/worker/tests) — the permission gate is the constraint
        }

        var boundTenant = await db.InsurerUsers.AsNoTracking()
            .Where(u => u.UserAccountId == new UserAccountId(accountId))
            .Select(u => (TenantId?)u.TenantId)
            .FirstOrDefaultAsync(ct);

        if (boundTenant is { } tenant)
        {
            if (tenant.Value != requestedTenantId)
            {
                throw new ForbiddenException("This insurer may only read their own tenant's cohort.");
            }

            return;
        }

        // Unbound: only oversight staff may read any tenant. The Insurer role alone grants nothing without a binding.
        var oversight = currentUser.Roles.Any(
            r => r != Role.Insurer && RolePermissions.Has(r, Permission.InsurerPortfolioView));
        if (!oversight)
        {
            throw new ForbiddenException("This account is not bound to an insurer tenant.");
        }
    }
}
