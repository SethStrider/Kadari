using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Access;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;

namespace Kadari.Application.Features.AssignTenant;

/// <summary>
/// Assign a customer's Kadari cohort to a tenant (D11) — the operational on-ramp from consumer-direct to an
/// insurer book. Upserts the customer's <see cref="TenantMembership"/> (so future captures inherit the tenant)
/// and re-stamps their existing assets onto it (so the insurer console sees the whole cohort). Administrative,
/// cross-customer — gated at the edge by <c>Permission.AssetTenantAssign</c>, so an <see cref="IUnscopedRequest"/>.
/// </summary>
public sealed record AssignCustomerToTenantCommand(Guid CustomerId, Guid TenantId)
    : ICommand<AssignCustomerToTenantResponse>, IUnscopedRequest;

/// <param name="AssetsReassigned">How many of the customer's existing assets were moved onto the tenant.</param>
public sealed record AssignCustomerToTenantResponse(Guid CustomerId, Guid TenantId, int AssetsReassigned);

internal sealed class AssignCustomerToTenantHandler(IKadariDbContext db, TimeProvider clock)
    : ICommandHandler<AssignCustomerToTenantCommand, AssignCustomerToTenantResponse>
{
    public async Task<AssignCustomerToTenantResponse> Handle(AssignCustomerToTenantCommand cmd, CancellationToken ct)
    {
        var customerId = new CustomerId(cmd.CustomerId);
        var tenantId = new TenantId(cmd.TenantId);
        var now = clock.GetUtcNow();

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, ct))
        {
            throw new NotFoundException(nameof(Tenant), cmd.TenantId);
        }

        if (!await db.Set<Customer>().AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException(nameof(Customer), cmd.CustomerId);
        }

        // Upsert the membership so future captures inherit the tenant.
        var membership = await db.TenantMemberships.FirstOrDefaultAsync(m => m.CustomerId == customerId, ct);
        if (membership is null)
        {
            db.TenantMemberships.Add(TenantMembership.Assign(customerId, tenantId, now));
        }
        else
        {
            membership.Reassign(tenantId, now);
        }

        // Re-stamp the customer's existing assets (via their backing subject) onto the tenant.
        var reassigned = 0;
        var subjectId = await db.Owners.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .Select(s => (OwnerId?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (subjectId is not null)
        {
            var assets = await db.Assets.Where(a => a.OwnerId == subjectId.Value).ToListAsync(ct);
            foreach (var asset in assets)
            {
                asset.ReassignTenant(tenantId);
            }

            reassigned = assets.Count;
        }

        await db.SaveChangesAsync(ct);

        return new AssignCustomerToTenantResponse(cmd.CustomerId, cmd.TenantId, reassigned);
    }
}
