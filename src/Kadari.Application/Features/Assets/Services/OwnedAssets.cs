using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Tenancy;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;
using Kadari.Domain.Owners;

namespace Kadari.Application.Features.Assets.Services;

/// <summary>
/// Finds an asset in a customer's own ledger — primary owner or household co-owner, inside the customer's tenant —
/// or throws a 404 (never 403, so ids are not leaked). The one place asset-level ownership is decided for the asset
/// and Care slices; the pipeline has already proven the caller is that customer.
/// </summary>
internal sealed class OwnedAssets(IKadariDbContext db)
{
    /// <summary>The asset, tracked, with its evidence — for commands.</summary>
    public async Task<Asset> GetForUpdateAsync(Guid customerId, Guid assetId, CancellationToken ct) =>
        await Query(customerId, assetId, tracked: true, ct)
        ?? throw new NotFoundException(nameof(Asset), assetId);

    /// <summary>The asset, untracked, with its evidence — for queries.</summary>
    public async Task<Asset> GetAsync(Guid customerId, Guid assetId, CancellationToken ct) =>
        await Query(customerId, assetId, tracked: false, ct)
        ?? throw new NotFoundException(nameof(Asset), assetId);

    /// <summary>The ids of every asset in the customer's ledger (owned or co-owned, inside their tenant).</summary>
    public async Task<IReadOnlyList<AssetId>> IdsAsync(Guid customerId, CancellationToken ct)
    {
        var customer = new CustomerId(customerId);
        var subject = await db.Owners.AsNoTracking()
            .Where(o => o.CustomerId == customer)
            .Select(o => (OwnerId?)o.Id)
            .FirstOrDefaultAsync(ct);
        if (subject is not { } owner)
        {
            return [];
        }

        var tenant = await db.TenantOfCustomerAsync(customer, ct);
        return await db.Assets.AsNoTracking()
            .Where(a => a.OwnerId == owner || a.CoOwners.Any(c => c.MemberId == owner))
            .ForTenant(tenant)
            .Select(a => a.Id)
            .ToListAsync(ct);
    }

    private async Task<Asset?> Query(Guid customerId, Guid assetId, bool tracked, CancellationToken ct)
    {
        var customer = new CustomerId(customerId);
        var subject = await db.Owners.AsNoTracking()
            .Where(o => o.CustomerId == customer)
            .Select(o => (OwnerId?)o.Id)
            .FirstOrDefaultAsync(ct);

        if (subject is not { } owner)
        {
            return null;
        }

        var tenant = await db.TenantOfCustomerAsync(customer, ct);
        var id = new AssetId(assetId);
        var assets = tracked ? db.Assets : db.Assets.AsNoTracking();

        return await assets
            .Include(a => a.Evidence)
            .Include(a => a.CoOwners)
            .Where(a => a.Id == id && (a.OwnerId == owner || a.CoOwners.Any(c => c.MemberId == owner)))
            .ForTenant(tenant)
            .FirstOrDefaultAsync(ct);
    }
}

/// <summary>A customer-scoped asset or Care request names its customer; that customer must exist (else 404).</summary>
internal static class CustomerExists
{
    public static async Task<Abstractions.Access.ResourceOwner?> ResolveAsync(
        IKadariDbContext db, Guid customerId, CancellationToken ct)
    {
        var exists = await db.Set<Customer>().AsNoTracking().AnyAsync(c => c.Id == new CustomerId(customerId), ct);
        return exists ? Abstractions.Access.ResourceOwner.Customer(customerId) : null;
    }
}
