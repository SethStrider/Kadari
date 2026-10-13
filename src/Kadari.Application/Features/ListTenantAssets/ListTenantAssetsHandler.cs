using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Common.Pagination;
using Kadari.Application.Exceptions;
using Kadari.Application.Features.Tenancy;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.ListTenantAssets;

internal sealed class ListTenantAssetsHandler(IKadariDbContext db, InsurerTenantGuard guard)
    : IQueryHandler<ListTenantAssetsQuery, PagedResult<TenantAssetRowDto>>
{
    public async Task<PagedResult<TenantAssetRowDto>> Handle(ListTenantAssetsQuery query, CancellationToken ct)
    {
        await guard.EnsureCanViewAsync(query.TenantId, ct);

        var tenantId = new TenantId(query.TenantId);

        // Settle existence: an unknown tenant is a 404, distinct from a real tenant with no assets.
        if (!await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct))
        {
            throw new NotFoundException(nameof(Tenant), query.TenantId);
        }

        var assets = db.Assets.AsNoTracking().Where(a => a.TenantId == tenantId);
        var totalCount = await assets.LongCountAsync(ct);

        // Sort fields are whitelisted (API standards §6). Assurance and best value are derived from the evidence
        // set in memory, so they are not sortable in SQL — only stored columns are.
        var ordered = (query.SortBy?.ToLowerInvariant(), query.Descending) switch
        {
            ("registered", true) => assets.OrderByDescending(a => a.RegisteredAt),
            ("registered", false) => assets.OrderBy(a => a.RegisteredAt),
            ("category", true) => assets.OrderByDescending(a => a.Category),
            ("category", false) => assets.OrderBy(a => a.Category),
            (_, true) => assets.OrderByDescending(a => a.DisplayName),
            (_, false) => assets.OrderBy(a => a.DisplayName),
        };

        var page = await ordered
            .Include(a => a.Evidence)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(ct);

        var items = page
            .Select(a =>
            {
                var value = a.BestReplacementValue;
                return new TenantAssetRowDto(
                    a.Id.Value, a.OwnerId.Value, a.DisplayName, a.Category.ToString(), a.Assurance.ToString(),
                    value?.AmountMinor, value?.Currency, a.Status.ToString(), a.RoomLabel);
            })
            .ToList();

        return PagedResult<TenantAssetRowDto>.From(items, query, totalCount);
    }
}
